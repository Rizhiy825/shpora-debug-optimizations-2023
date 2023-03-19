using System;
using System.Drawing;
using System.Numerics;
using JPEG.Images;

namespace JPEG.Algorithmes;

public class FFT2DNew
{
    private FFTNew fft;
    public FFT2DNew()
    {
        fft = new FFTNew(8);
    }

    public Complex[][] ToComplex(Matrix image, Func<Pixel, double> channelSelector)
    {
        var result = new Complex[image.Height][];

        for (var y = 0; y < image.Height; y++)
        {
            result[y] = new Complex[image.Width];

            for (var x = 0; x < image.Width; x++)
            {
                var pixel = new Complex(channelSelector(image.Pixels[y, x]), 0);

                result[y][x] = pixel;
            }
        }
        return result;
    }

    public double[,] Inverse(Complex[][] inputComplex)
    {
        var height = inputComplex.GetLength(0);
        var p = new Complex[height][];
        var f = new Complex[height][];
        var t = new Complex[height][];

        var floatImage = new double[height, height];

        //CALCULATE P
        for (var l = 0; l < height; l++)
        {
            var input = inputComplex[l];
            p[l] = fft.Inverse(input);
        }

        //TRANSPOSE AND COMPUTE
        for (var l = 0; l < height; l++)
        {
            t[l] = new Complex[height];

            for (var k = 0; k < height; k++)
            {
                t[l][k] = p[k][l] / (height * height);
            }

            f[l] = fft.Inverse(t[l]);
        }

        for (var k = 0; k < height; k++)
        {
            for (var l = 0; l < height; l++)
            {
                floatImage[k, l] = f[k][l].Magnitude;  //Math.Abs(f[k][l].Real);
            }
        }
        return floatImage;
    }

    public Complex[][] Forward(Matrix image, Func<Pixel, double> channelSelector)
    {
        var p = new Complex[image.Width][];
        var f = new Complex[image.Width][];
        var t = new Complex[image.Width][];

        //CONVERT TO COMPLEX NUMBERS
        var complexImage = ToComplex(image, channelSelector);

        //CALCULATE P
        for (var l = 0; l < complexImage.GetLength(0); l++)
        {
            p[l] = fft.Forward(complexImage[l]);
        }

        //TANSPOSE AND COMPUTE
        for (var l = 0; l < complexImage.GetLength(0); l++)
        {
            t[l] = new Complex[complexImage.GetLength(0)];

            for (var k = 0; k < complexImage.GetLength(0); k++)
            {
                t[l][k] = p[k][l];
            }

            f[l] = fft.Forward(t[l]);
        }

        return f;
    }
}