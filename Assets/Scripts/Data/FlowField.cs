using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowFields.Data
{
    public struct FlowField : IDisposable
    {
        public int2 targetTile;
        public bool isValid;
        public NativeArray<float> stepField;
        public NativeArray<float2> flowField;
        public NativeList<NextIndex> openSet;
        public NativeList<NextIndex> nextSet;

        public MapData mapData;

        public FlowField(int2 target, MapData map)
        {
            mapData = map;
            targetTile = target;
            nextSet = new NativeList<NextIndex>(Allocator.Persistent);
            openSet = new NativeList<NextIndex>(Allocator.Persistent);
            stepField = new NativeArray<float>(mapData.width * mapData.height, Allocator.Persistent);
            flowField = new NativeArray<float2>(mapData.width * mapData.height, Allocator.Persistent);

            isValid = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTarget(int2 target)
        {
            targetTile = target;
            ClearSets();
            openSet.Add(new NextIndex {index = target.x + mapData.width * target.y, codeModifier = 0});
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearSets()
        {
            openSet.Clear();
            nextSet.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 GetFlowField(int2 tile)
        {
            return flowField[tile.x + tile.y * mapData.width];
        }

        public void Dispose()
        {
            stepField.Dispose();
            flowField.Dispose();
            openSet.Dispose();
            nextSet.Dispose();
            isValid = false;
        }
    }

    public struct NextIndex
    {
        public int index;
        public byte codeModifier;
    }
}