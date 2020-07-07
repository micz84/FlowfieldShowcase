using Unity.Entities;
using Unity.Mathematics;

namespace FlowFields.Components
{
    public struct Sector:IComponentData
    {
        public int2 coordinates;
        public override string ToString()
        {
            return $"[{coordinates.x}][{coordinates.y}]";
        }
    }
}