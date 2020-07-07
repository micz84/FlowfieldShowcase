using Unity.Entities;
using Unity.Mathematics;

namespace FlowFields.Components
{
    public struct MapTile:IComponentData
    {
        public byte moveCost;
        public byte agents;
        public byte availableDirectionsCode;
    }
}