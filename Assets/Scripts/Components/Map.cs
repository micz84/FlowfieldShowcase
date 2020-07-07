using Unity.Entities;

namespace FlowFields.Components
{
    public struct Map:IComponentData
    {
        public int width;
        public int height;
    }
}