using System.Collections.Generic;
using JPEG.Processor;
using System.Numerics;

namespace JPEG.Utilities;

public static class Buffer
{
    public static Complex[][] complexes = InitComplexes(JpegProcFFTV3.DCTSize, JpegProcFFTV3.DCTSize);
    public static Complex[][] bufferP = InitComplexes(JpegProcFFTV3.DCTSize, JpegProcFFTV3.DCTSize);
    public static Complex[][] bufferT = InitComplexes(JpegProcFFTV3.DCTSize, JpegProcFFTV3.DCTSize);
    public static Complex[][] bufferF = InitComplexes(JpegProcFFTV3.DCTSize, JpegProcFFTV3.DCTSize);

    public static byte[,] quantizedFreqs = new byte[JpegProcFFTV3.DCTSize, JpegProcFFTV3.DCTSize];
    public static byte[] quantizedBytes = new byte[JpegProcFFTV3.DCTSize * JpegProcFFTV3.DCTSize];

    public static Complex[] forwardDoubleBuffer = new Complex[2];
    public static List<Complex[]> forwardSingleBuffers = InitSingleBuffers();

    private static Complex[][] InitComplexes(int height, int width)
    {
        var matrix = new Complex[height][];

        for (int y = 0; y < height; y++)
        {
            matrix[y] = new Complex[width];
        }

        return matrix;
    }

    private static List<Complex[]> InitSingleBuffers()
    {
        var buffers = new List<Complex[]>();

        for (int i = 0; i < 4; i++)
        {
            buffers.Add(new Complex[1]);
        }

        return buffers;
    }
}