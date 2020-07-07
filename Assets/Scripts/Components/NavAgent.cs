using System;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowFields.Components
{
    public struct NavAgent:IComponentData
    {

        public float radius;
        public float moveSpeed;
        public float moveSharpness;
        public float decollisionDamping;
        public float orientSharpness;
        public float3 avoidanceVector;

        [NonSerialized]
        public float3 storedImpulse;

        [NonSerialized] public float obstacleDistanceFactor;

    }
}