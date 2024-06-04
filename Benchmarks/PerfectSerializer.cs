namespace Benchmarks;

public class PerfectSerializer
{
#if NET8_0
    [System.Runtime.CompilerServices.InlineArray(16)]
    public struct FixeSizeByteBuffer16
    {
        private byte item;
    }

    [System.Runtime.CompilerServices.InlineArray(8)]
    public struct FixeSizeByteBuffer8
    {
        private byte item;
    }

    public struct InnerMessageStruct
    {
        public int id;
        public FixeSizeByteBuffer16 name;
    }

    [System.Runtime.CompilerServices.InlineArray(9)]
    public struct FixeSizeInnerMessageBuffer9
    {
        private InnerMessageStruct item;
    }

    public struct RootMessageStruct
    {
        public ulong a;
        public long b;
        public FixeSizeByteBuffer16 d;
        public FixeSizeByteBuffer8 e1;
        public FixeSizeByteBuffer8 e2;
        public FixeSizeByteBuffer8 e3;
        public FixeSizeByteBuffer8 e4;
        public FixeSizeInnerMessageBuffer9 f;
    }
#endif
}