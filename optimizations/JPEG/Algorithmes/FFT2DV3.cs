using JPEG.Images;
using System.Numerics;
using System;
using System.Drawing;
using JPEG.Processor;

namespace JPEG.Algorithmes;

public class FFT2DV3
{
    private FFTV3 fft;
    public FFT2DV3()
    {
        fft = new FFTV3(8);
    }

    public Complex[][] ToComplex(Bitmap image, int yOffset, int xOffset, Func<Color, double> channelSelector, Complex[][] complexes)
    {
        var edgeSize = JpegProcFFTV3.DCTSize;

        for (var y = 0; y < edgeSize; y++)
        {
            for (var x = 0; x < edgeSize; x++)
            {
                lock (image)
                {
                    complexes[y][x] = new Complex(channelSelector(image.GetPixel(xOffset + x, yOffset + y)), 0);
                }
            }
        }
        return complexes;
    }

    public double[,] Inverse(Complex[][] inputComplex, Complex[][] bufferP, Complex[][] bufferF, Complex[][] bufferT, double[,] map)
    {
        var height = JpegProcFFTV3.DCTSize;

        //CALCULATE P
        for (var l = 0; l < height; l++)
        {
            var input = inputComplex[l];
            bufferP[l] = fft.Forward(input, bufferP[l], false);
        }

        //TRANSPOSE AND COMPUTE
        for (var l = 0; l < height; l++)
        {
            for (var k = 0; k < height; k++)
            {
                bufferT[l][k] = bufferP[k][l] / (height * height);
            }

            bufferF[l] = fft.Forward(bufferT[l], bufferF[l], false);
        }

        for (var k = 0; k < height; k++)
        {
            for (var l = 0; l < height; l++)
            {
                map[k, l] = bufferF[k][l].Magnitude;  //Math.Abs(f[k][l].Real);
            }
        }
        return map;
    }

    public Complex[][] Forward(Bitmap image, int yOffset, int xOffset, Func<Color, double> channelSelector, Complex[][] complexes, Complex[][] bufferP, Complex[][] bufferF, Complex[][] bufferT)
    {
        //CONVERT TO COMPLEX NUMBERS
        complexes = ToComplex(image, yOffset, xOffset, channelSelector, complexes);
        var edge = JpegProcFFTV3.DCTSize;

        //CALCULATE P
        for (var l = 0; l < edge; l++)
        {
            bufferP[l] = fft.Forward(complexes[l], bufferP[l]);
        }

        //TANSPOSE AND COMPUTE
        for (var l = 0; l < edge; l++)
        {
            for (var k = 0; k < edge; k++)
            {
                bufferT[l][k] = bufferP[k][l];
            }

            bufferF[l] = fft.Forward(bufferT[l], bufferF[l]);
        }

        return bufferF;
    }
}