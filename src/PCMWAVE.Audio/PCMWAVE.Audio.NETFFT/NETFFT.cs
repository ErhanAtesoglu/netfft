namespace PCMWAVE.Audio.NETFFT
{
    public class NETFFT
    {
        public static class FFT
        {
            // Scalar real (your primary audio path)
            public static void RealFFTScalar(ReadOnlySpan<float> input, Span<float> magnitude)
            { }
            // Scalar complex *FUTURE*
            public static void ComplexFFTScalar(ReadOnlySpan<float> realIn, ReadOnlySpan<float> imaginaryIn,
                                               Span<float> realOut, Span<float> imagOut)
            { }

            // Real Valued Vectorized dispatcher this is the one you want to call,
            // it will dispatch to the appropriate implementation based on hardware support
            public static void RealFFT(ReadOnlySpan<float> input, Span<float> magnitude)
            { }

            // Complex Valued Vectorized dispatcher this is the one you want to call,
            // it will dispatch to the appropriate implementation based on hardware support
            public static void ComplexFFT(ReadOnlySpan<float> realIn, ReadOnlySpan<float> imaginaryIn,
                                          Span<float> realOut, Span<float> imaginaryOut)
            { }
        }
    }
}
