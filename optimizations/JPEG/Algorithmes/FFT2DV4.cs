using JPEG.Processor;
using System.Drawing;
using System.Numerics;
using System;
using System.Runtime.CompilerServices;

namespace JPEG.Algorithmes;

public class FFT2DV4
{
    public Complex[][] ToComplex(byte[] image, int yOffset, int xOffset, int channelSelector, Complex[][] complexes)
    {
        var edgeSize = JpegProcFFTV3.DCTSize;

        for (var y = 0; y < edgeSize; y++)
        {
            for (var x = 0; x < edgeSize; x++)
            {
                var value = GetPixelFromImage(image, y + yOffset, x + xOffset, channelSelector);
                complexes[y][x] = new Complex(value, 0);
            }
        }
        return complexes;
    }

    public double[,] Inverse(Complex[][] inputComplex, Complex[][] bufferP, Complex[][] bufferF, Complex[][] bufferT, double[,] map, Complex polarComplex)
    {
        var height = JpegProcFFTV3.DCTSize;
        
        for (var l = 0; l < height; l++)
        {
            var input = inputComplex[l];
            bufferP[l] = Forward(input, bufferP[l], polarComplex);
        }
        
        for (var l = 0; l < height; l++)
        {
            for (var k = 0; k < height; k++)
            {
                bufferT[l][k] = bufferP[k][l] / (height * height);
            }

            bufferF[l] = Forward(bufferT[l], bufferF[l], polarComplex);
        }

        for (var k = 0; k < height; k++)
        {
            for (var l = 0; l < height; l++)
            {
                map[k, l] = bufferF[k][l].Magnitude; 
            }
        }
        return map;
    }

    public Complex[][] Forward(byte[] image, 
        int yOffset, 
        int xOffset, 
        int channelSelector, 
        Complex[][] complexes, 
        Complex[][] bufferP, 
        Complex[][] bufferF, 
        Complex[][] bufferT, 
        Complex polarComplex)
    {
        complexes = ToComplex(image, yOffset, xOffset, channelSelector, complexes);
        var dctDize = JpegProcFFTV3.DCTSize;
        
        for (var l = 0; l < dctDize; l++)
        {
            bufferP[l] = Forward(complexes[l], bufferP[l], polarComplex);
        }

        for (var l = 0; l < dctDize; l++)
        {
            for (var k = 0; k < dctDize; k++)
            {
                bufferT[l][k] = bufferP[k][l];
            }

            bufferF[l] = Forward(bufferT[l], bufferF[l], polarComplex);
        }

        return bufferF;
    }

    private double GetPixelFromImage(byte[] image, int y, int x, int channel)
    {
        var index = y * JpegProcFFTParallel.stride + x * 3 + channel;
        var value = image[index];
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Complex[] Forward(Complex[] input, Complex[] buffer, Complex polarComplex)
    {
        input[1] *= polarComplex;
        buffer[0] = input[0] + input[1];
        buffer[1] = input[0] - input[1];
        return buffer;
    }
}