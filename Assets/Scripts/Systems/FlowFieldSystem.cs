using FlowFields.Components;
using FlowFields.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlowFields.Systems
{
    //[UpdateInGroup(typeof(InitializationSystemGroup))]
    // [UpdateAfter(typeof(MapGenerationSystem))]
    //[AlwaysUpdateSystem]
    public class FlowFieldSystem : JobComponentSystem
    {
        private NativeMultiHashMap<int2, int> counts;
        public FlowField flowField;
        public JobHandle generateField;
        private MapGenerationSystem mapSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            mapSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<MapGenerationSystem>();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (flowField.isValid)
                flowField.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!mapSystem.mapData.IsValid)
                return inputDeps;

            generateField.Complete();
            var calculateAgentTile = new CalculateNavAgentTile();
            var handle = calculateAgentTile.Schedule(this, inputDeps);
            var clearTileAgentsCount = new ClearTileAgentsCount();
            var handle2 = clearTileAgentsCount.Schedule(this, inputDeps);

            handle = JobHandle.CombineDependencies(handle, handle2);
            if (!counts.IsCreated)
                counts = new NativeMultiHashMap<int2, int>(mapSystem.mapData.width * mapSystem.mapData.height,
                    Allocator.TempJob);
            counts.Clear();
            handle = new CountJob
            {
                hashMap = counts.AsParallelWriter()
            }.Schedule(this, handle);

            handle = new UpdateCounts
            {
                hashMap = counts
            }.Schedule(this, handle);

            var updateJob = new UpdateMapCache
            {
                mapTiles = mapSystem.mapData.tiles,
                width = mapSystem.mapData.width
            };
            handle = updateJob.Schedule(this, handle);


            if (!flowField.isValid)
            {
                var target = new int2(mapSystem.mapData.width / 2 + 1, mapSystem.mapData.height / 2 + 1);

                flowField = new FlowField(target, mapSystem.mapData);
            }

            handle = new SetTargetJob
            {
                target = flowField.targetTile,
                flowField = flowField
            }.Schedule(handle);

            var initJob = new InitializeFieldJob
            {
                stepField = flowField.stepField,
                flowField = flowField.flowField,
                target = flowField.targetTile.x + flowField.targetTile.y * mapSystem.mapData.width
            };

            handle = initJob.Schedule(flowField.flowField.Length, 64, handle);

            var generateCostFieldJob = new GenerateCostField
            {
                map = mapSystem.mapData,
                blob = mapSystem.mapData.mapDataBlob,
                stepField = flowField.stepField,
                openSet = flowField.openSet,
                nextSet = flowField.nextSet
            };

            handle = generateCostFieldJob.Schedule(handle);
            var generateFlowFieldJob = new GenerateFlowField
            {
                map = mapSystem.mapData,
                stepField = flowField.stepField,
                flowField = flowField.flowField,
                blob = mapSystem.mapData.mapDataBlob
            };
            generateField = generateFlowFieldJob.Schedule(flowField.flowField.Length, 64, handle);
            return generateField;
        }

        [BurstCompile]
        private struct SetTargetJob : IJob
        {
            public int2 target;
            public FlowField flowField;

            public void Execute()
            {
                flowField.SetTarget(target);
            }
        }

        [BurstCompile]
        private struct InitializeFieldJob : IJobParallelFor
        {
            public NativeArray<float> stepField;
            public NativeArray<float2> flowField;
            public int target;

            public void Execute(int index)
            {
                stepField[index] = math.select(float.MaxValue, 0, index == target);
                flowField[index] = float2.zero;
            }
        }

        [BurstCompile]
        private struct GenerateCostField : IJob
        {
            [ReadOnly] public MapData map;
            [ReadOnly] public BlobAssetReference<MapDataBlob> blob;
            public NativeArray<float> stepField;
            public NativeList<NextIndex> openSet;
            public NativeList<NextIndex> nextSet;

            public void Execute()
            {
                while (openSet.Length > 0)
                {
                    // repeat until out of tiles
                    for (var j = 0; j < openSet.Length; j++)
                    {
                        var indexWithDir = openSet[j];
                        var index = indexWithDir.index;
                        var existingCost = stepField[index];
                        var code = (byte) (map.tiles[index].availableDirectionsCode | indexWithDir.codeModifier);
                        var dirs = blob.Value.CodeStartEndIndex[code];

                        for (var i = dirs.startIndex; i < dirs.endIndex; i++)
                        {
                            var directionData = blob.Value.DirectionData[i];
                            var newIndex = index + directionData.MoveDirectionIndexOffset;
                            var mapTile = map.tiles[newIndex];
                            var moveCost = mapTile.moveCost + mapTile.agents * 0.1f;
                            var cost = moveCost * directionData.DirectionLength + existingCost;
                            if (cost >= stepField[newIndex] * 0.98f) continue;
                            stepField[newIndex] = cost;
                            nextSet.Add(
                                new NextIndex {index = newIndex, codeModifier = directionData.CodeModifications});
                        }
                    }

                    var temp = openSet;
                    openSet = nextSet;
                    nextSet = temp;
                    nextSet.Clear();
                }
            }
        }

        [BurstCompile]
        private struct GenerateFlowField : IJobParallelFor
        {
            [ReadOnly] public MapData map;
            [ReadOnly] public NativeArray<float> stepField;
            public NativeArray<float2> flowField;
            [ReadOnly] public BlobAssetReference<MapDataBlob> blob;

            public void Execute(int index)
            {
                if (map.IsWall(index))
                    return;
                var myCost = stepField[index];
                var minNeighborCost = myCost;
                var bestNeighborMoveDir = new float2(0, 0);
                var valid = false;
                var code = map.tiles[index]
                    .availableDirectionsCode;
                var dirs = blob.Value.CodeStartEndIndex[code];
                for (var i = dirs.startIndex; i < dirs.endIndex; i++)
                {
                    var directionData = blob.Value.DirectionData[i];
                    var cost = stepField[index + directionData.MoveDirectionIndexOffset];
                    var checkCost = cost < minNeighborCost;
                    minNeighborCost = math.min(minNeighborCost, cost);
                    bestNeighborMoveDir = math.select(bestNeighborMoveDir, directionData.MoveDirection, checkCost);
                    valid = true;
                }

                flowField[index] = math.select(float2.zero, bestNeighborMoveDir, valid);
                ;
            }
        }

        [BurstCompile]
        [RequireComponentTag(typeof(NavAgent))]
        private struct CalculateNavAgentTile : IJobForEach<Sector, Translation>
        {
            public void Execute(ref Sector sector, [ReadOnly] ref Translation translation)
            {
                sector.coordinates = new int2((int) math.floor(translation.Value.x),
                    (int) math.floor(translation.Value.z));
            }
        }

        [BurstCompile]
        private struct ClearTileAgentsCount : IJobForEach<MapTile>
        {
            public void Execute(ref MapTile tile)
            {
                tile.agents = 0;
            }
        }

        [BurstCompile]
        private struct UpdateMapCache : IJobForEach<MapTile, Sector>
        {
            public int width;

            [NativeDisableParallelForRestriction] public NativeArray<MapTile> mapTiles;

            public void Execute([ReadOnly] ref MapTile tile, [ReadOnly] ref Sector sector)
            {
                mapTiles[sector.coordinates.x + sector.coordinates.y * width] = tile;
            }
        }

        [BurstCompile]
        [RequireComponentTag(typeof(NavAgent))]
        private struct CountJob : IJobForEach<Sector>
        {
            public NativeMultiHashMap<int2, int>.ParallelWriter hashMap;

            public void Execute([ReadOnly] ref Sector agent)
            {
                hashMap.Add(agent.coordinates, 1);
            }
        }

        [BurstCompile]
        private struct UpdateCounts : IJobForEach<MapTile, Sector>
        {
            [ReadOnly] public NativeMultiHashMap<int2, int> hashMap;

            public void Execute(ref MapTile tile, [ReadOnly] ref Sector sector)
            {
                tile.agents = (byte) hashMap.CountValuesForKey(sector.coordinates);
            }
        }
    }
}