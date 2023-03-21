using System.Collections.Generic;
using JPEG.Processor;
using System.Numerics;
using System;

namespace JPEG.Utilities;

public static class Buffer
{
    public static Complex[][] complexes = InitComplexes();
    public static Complex[][] bufferP = InitComplexes();
    public static Complex[][] bufferT = InitComplexes();
    public static Complex[][] bufferF = InitComplexes();

    public static byte[,] quantizedFreqs = new byte[JpegProcFFTV3.DCTSize, JpegProcFFTV3.DCTSize];
    public static byte[] quantizedBytes = new byte[JpegProcFFTV3.DCTSize * JpegProcFFTV3.DCTSize];
    
    public const float Omega = (float)(-2.0 * Math.PI / 2.0);

    private static Complex[][] InitComplexes()
    {
        var matrix = new Complex[JpegProcFFTParallel.DCTSize][];

        for (int y = 0; y < JpegProcFFTParallel.DCTSize; y++)
        {
            matrix[y] = new Complex[JpegProcFFTParallel.DCTSize];
        }

        return matrix;
    }
}