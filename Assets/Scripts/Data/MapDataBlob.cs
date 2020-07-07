using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowFields.Data
{
    public static class MapDataBlobFactory
    {
        public static BlobAssetReference<MapDataBlob> Generate(int width)
        {
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var mapDataBlob = ref blobBuilder.ConstructRoot<MapDataBlob>();
            AllocateCodeIndexes(ref blobBuilder, ref mapDataBlob, width);
            AllocateDirectionOffset(ref blobBuilder, ref mapDataBlob, width);
            AllocateMoveDirections(ref blobBuilder, ref mapDataBlob);
            AllocateMoveDirectionsLengths(ref blobBuilder, ref mapDataBlob);
            AllocateCodeModifications(ref blobBuilder, ref mapDataBlob);

            var blobAssetReference = blobBuilder.CreateBlobAssetReference<MapDataBlob>(Allocator.Persistent);
            blobBuilder.Dispose();
            return blobAssetReference;
        }

        private static void AllocateCodeIndexes(ref BlobBuilder blobBuilder, ref MapDataBlob mapDataBlob, int width)
        {
            var codeStartEndIndex = blobBuilder.Allocate(ref mapDataBlob.CodeStartEndIndex, 256);
            var directionIndexes = blobBuilder.Allocate(ref mapDataBlob.DirectionIndex, 1024);
            var directionDatas = blobBuilder.Allocate(ref mapDataBlob.DirectionData, 1024);
            var directionLength = new float[8];
            directionLength[0] = 1;
            directionLength[1] = math.SQRT2;
            directionLength[2] = 1;
            directionLength[3] = math.SQRT2;
            directionLength[4] = 1;
            directionLength[5] = math.SQRT2;
            directionLength[6] = 1;
            directionLength[7] = math.SQRT2;
            var moveDirections = new int2[8];
            moveDirections[0] = new int2(0, 1);
            moveDirections[1] = new int2(1, 1);
            moveDirections[2] = new int2(1, 0);
            moveDirections[3] = new int2(1, -1);
            moveDirections[4] = new int2(0, -1);
            moveDirections[5] = new int2(-1, -1);
            moveDirections[6] = new int2(-1, 0);
            moveDirections[7] = new int2(-1, 1);

            var moveDirectionIndexOffset = new int[8];
            moveDirectionIndexOffset[0] = width;
            moveDirectionIndexOffset[1] = 1 + width;
            moveDirectionIndexOffset[2] = 1;
            moveDirectionIndexOffset[3] = 1 - width;
            moveDirectionIndexOffset[4] = -width;
            moveDirectionIndexOffset[5] = -1 - width;
            moveDirectionIndexOffset[6] = -1;
            moveDirectionIndexOffset[7] = -1 + width;

            var currentIndex = 0;
            for (var code = 0; code < 256; code++)
            {
                var codeData = new CodeData();
                codeData.startIndex = (ushort) currentIndex;
                for (byte directionIndex = 0; directionIndex < 8; directionIndex++)
                {
                    if ((code & (1 << directionIndex)) != 0) continue;
                    directionIndexes[currentIndex] = directionIndex;
                    var directionData = new DirectionData();
                    directionData.CodeModifications = GetCodeModification(directionIndex);
                    directionData.DirectionLength = directionLength[directionIndex];
                    directionData.MoveDirectionIndexOffset = (short) moveDirectionIndexOffset[directionIndex];
                    directionData.MoveDirection = math.normalize(new float2(moveDirections[directionIndex].x,
                        moveDirections[directionIndex].y));
                    directionDatas[currentIndex] = directionData;

                    currentIndex++;
                }

                codeData.endIndex = (ushort) currentIndex;
                codeStartEndIndex[code] = codeData;
            }
        }

        private static void AllocateMoveDirectionsLengths(ref BlobBuilder blobBuilder, ref MapDataBlob mapDataBlob)
        {
            var directionLength = blobBuilder.Allocate(ref mapDataBlob.DirectionLength, 8);
            directionLength[0] = 1;
            directionLength[1] = math.SQRT2;
            directionLength[2] = 1;
            directionLength[3] = math.SQRT2;
            directionLength[4] = 1;
            directionLength[5] = math.SQRT2;
            directionLength[6] = 1;
            directionLength[7] = math.SQRT2;
        }

        private static void AllocateMoveDirections(ref BlobBuilder blobBuilder, ref MapDataBlob mapDataBlob)
        {
            var moveDirections = blobBuilder.Allocate(ref mapDataBlob.MoveDirections, 8);
            moveDirections[0] = new int2(0, 1);
            moveDirections[1] = new int2(1, 1);
            moveDirections[2] = new int2(1, 0);
            moveDirections[3] = new int2(1, -1);
            moveDirections[4] = new int2(0, -1);
            moveDirections[5] = new int2(-1, -1);
            moveDirections[6] = new int2(-1, 0);
            moveDirections[7] = new int2(-1, 1);
        }

        private static void AllocateDirectionOffset(ref BlobBuilder blobBuilder, ref MapDataBlob mapDataBlob, int width)
        {
            var moveDirectionIndexOffset = blobBuilder.Allocate(ref mapDataBlob.MoveDirectionIndexOffset, 8);
            moveDirectionIndexOffset[0] = width;
            moveDirectionIndexOffset[1] = 1 + width;
            moveDirectionIndexOffset[2] = 1;
            moveDirectionIndexOffset[3] = 1 - width;
            moveDirectionIndexOffset[4] = -width;
            moveDirectionIndexOffset[5] = -1 - width;
            moveDirectionIndexOffset[6] = -1;
            moveDirectionIndexOffset[7] = -1 + width;
        }

        private static void AllocateCodeModifications(ref BlobBuilder blobBuilder, ref MapDataBlob mapDataBlob)
        {
            var codeModifications = blobBuilder.Allocate(ref mapDataBlob.CodeModifications, 8);
            for (var i = 0; i < 8; i++) codeModifications[i] = GetCodeModification(i);
        }

        private static byte GetCodeModification(int i)
        {
            byte mod = 0;
            switch (i)
            {
                case 0:
                    mod |= 16;
                    mod |= 64; //(byte) math.select(0, 64, (code & 128) == 0);
                    mod |= 4; //(byte) math.select(0, 4, (code & 2) == 0);
                    mod |= 8; //(byte) math.select(0, 8, (code & 4) == 0);
                    mod |= 32; //(byte) math.select(0, 32, (code & 64) == 0);
                    break;
                case 1:
                    mod |= 64; //(byte) math.select(0, 64, (code & 1) == 0);
                    mod |= 16; //(byte) math.select(0, 16, (code & 4) == 0);
                    mod |= 32;
                    break;
                case 2:
                    mod |= 1; //(byte) math.select(0, 1, (code & 2) == 0);
                    mod |= 16; //(byte) math.select(0, 16, (code & 8) == 0);
                    mod |= 128;
                    mod |= 32;
                    mod |= 64;
                    break;
                case 3:
                    mod |= 1; //(byte) math.select(0, 1, (code & 4) == 0);
                    mod |= 64; //(byte) math.select(0, 64, (code & 16) == 0);
                    mod |= 128;
                    break;
                case 4:
                    mod |= 4; //(byte) math.select(0, 4, (code & 8) == 0);
                    mod |= 64; //(byte) math.select(0, 64, (code & 32) == 0);
                    mod |= 2;
                    mod |= 128;
                    mod |= 1;
                    break;
                case 5:
                    mod |= 1; //(byte) math.select(0, 4, (code & 16) == 0);
                    mod |= 4; //(byte) math.select(0, 64, (code & 64) == 0);
                    mod |= 2;
                    break;
                case 6:
                    mod |= 16; //(byte) math.select(0, 16, (code & 32) == 0);
                    mod |= 1; //(byte) math.select(0, 1, (code & 128) == 0);
                    mod |= 2;
                    mod |= 8;
                    mod |= 4;
                    break;
                case 7:
                    mod |= 16; //(byte) math.select(0, 16, (code & 64) == 0);
                    mod |= 4; //(byte) math.select(0, 4, (code & 1) == 0);
                    mod |= 8;
                    break;
            }

            return mod;
        }
    }

    public struct MapDataBlob
    {
        /// <summary>
        ///     Holds start and end index in DirectionIndex for each code
        /// </summary>
        public BlobArray<CodeData> CodeStartEndIndex;

        /// <summary>
        ///     Holds indexes for directions for all codes.
        ///     Indexes are used to access data from MoveDirectionIndexOffset, MoveDirections, DirectionLength
        /// </summary>
        public BlobArray<byte> DirectionIndex;

        public BlobArray<int> MoveDirectionIndexOffset;
        public BlobArray<int2> MoveDirections;
        public BlobArray<float> DirectionLength;
        public BlobArray<byte> CodeModifications;
        public BlobArray<DirectionData> DirectionData;
    }

    public struct CodeData
    {
        public int startIndex;
        public int endIndex;
    }

    public struct DirectionData
    {
        public short MoveDirectionIndexOffset;
        public float DirectionLength;
        public byte CodeModifications;
        public float2 MoveDirection;
    }
}