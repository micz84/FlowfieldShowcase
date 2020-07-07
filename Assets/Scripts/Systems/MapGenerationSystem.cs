using FlowFields.Components;
using FlowFields.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowFields.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    //[AlwaysUpdateSystem]
    public class MapGenerationSystem : JobComponentSystem
    {
        //public JobHandle generateMapHandle;
        private EndInitializationEntityCommandBufferSystem _barier;
        public MapData mapData;
        private EntityQuery query;
        private EntityQuery queryTiles;
        private Random random;
        private EntityArchetype tileArchetype;

        protected override void OnCreate()
        {
            base.OnCreate();
            _barier = World.DefaultGameObjectInjectionWorld
                .GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            var desc = new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadWrite<Map>()}
            };
            query = EntityManager.CreateEntityQuery(desc);
            var tilesDesc = new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadWrite<MapTile>()}
            };
            queryTiles = EntityManager.CreateEntityQuery(tilesDesc);
            tileArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<MapTile>());
            random = new Random(42);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            mapData.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (mapData.IsValid) return inputDeps;

            var maps = query.ToComponentDataArray<Map>(Allocator.TempJob);
            if (maps.Length == 0)
            {
                maps.Dispose();
                return inputDeps;
            }

            var map = maps[0];
            maps.Dispose();
            mapData = new MapData(map.width, map.height);

            var updateMap = new GenerateMapJob
            {
                mapTiles = mapData.tiles,
                width = mapData.width
            };
            inputDeps = updateMap.Schedule(this, inputDeps);
            var directions = new GenerateDirectionsJob
            {
                map = mapData
            };
            inputDeps = directions.Schedule(this, inputDeps);

            _barier.AddJobHandleForProducer(inputDeps);
            return inputDeps;
        }

        private struct GenerateDirectionsJob : IJobForEachWithEntity<MapTile, Sector>
        {
            [ReadOnly] public MapData map;

            public void Execute(Entity entity, int index, ref MapTile mapTile, [ReadOnly] ref Sector sector)
            {
                var x = sector.coordinates.x;
                var y = sector.coordinates.y;
                var l = 8;
                byte code = 0;
                byte one = 1;
                if (map.IsWall(sector.coordinates))
                {
                    mapTile.availableDirectionsCode = byte.MaxValue;
                    return;
                }

                for (var i = 0; i < l; i++)
                {
                    var moveDir = map.mapDataBlob.Value.MoveDirections[i];
                    var newTile = new int2(x + moveDir.x, y + moveDir.y);
                    if (!map.IsInsideMap(newTile) || map.IsWall(newTile)) code += (byte) (one << i);
                }

                mapTile.availableDirectionsCode = code;
            }
        }

        [BurstCompile]
        private struct GenerateMapJob : IJobForEach<MapTile, Sector>
        {
            [NativeDisableParallelForRestriction] public NativeArray<MapTile> mapTiles;

            public int width;


            public void Execute(ref MapTile c0, ref Sector c1)
            {
                mapTiles[c1.coordinates.x + c1.coordinates.y * width] = c0;
            }
        }
    }
}