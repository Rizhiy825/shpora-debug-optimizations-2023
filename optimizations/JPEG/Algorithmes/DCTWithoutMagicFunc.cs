using JPEG.Utilities;
using System;
using System.Threading.Tasks;

namespace JPEG.Algorithmes;

public class DCTWithoutMagicFunc
{
    public static double[,] DCT2D(double[,] input, double[,] outputCoeffs)
    {
        var height = input.GetLength(0);
        var width = input.GetLength(1);

        Parallel.For(0, height, SumVars);

        void SumVars(int u)
        {
            for (int v = 0; v < height; v++)
            {
                var sum = 0d;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        sum += BasisFunction(input[x, y], u, v, x, y, height, width);
                    }
                }

                outputCoeffs[u, v] = sum * Beta(height, width) * Alpha(u) * Alpha(v);
            }
        }

        return outputCoeffs;
    }

    public static double[,] IDCT2D(double[,] coeffs, double[,] output)
    {
        Parallel.For(0, coeffs.GetLength(1), SumVars);

        void SumVars(int x)
        {
            for (var y = 0; y < coeffs.GetLength(0); y++)
            {
                var sum = 0d;

                for (int u = 0; u < coeffs.GetLength(1); u++)
                {
                    for (int v = 0; v < coeffs.GetLength(0); v++)
                    {
                        sum += BasisFunction(coeffs[u, v], u, v, x, y, coeffs.GetLength(0), coeffs.GetLength(1)) *
                               Alpha(u) * Alpha(v);
                    }
                }

                output[x, y] = sum * DCTDefault.Beta(coeffs.GetLength(0), coeffs.GetLength(1));
            }
        }

        return output;
    }

    public static double BasisFunction(double a, double u, double v, double x, double y, int height, int width)
    {
        var b = Math.Cos((2d * x + 1d) * u * Math.PI / (2 * width));
        var c = Math.Cos((2d * y + 1d) * v * Math.PI / (2 * height));

        return a * b * c;
    }

    public static double Alpha(int u)
    {
        if (u == 0)
            return 1 / Math.Sqrt(2);
        return 1;
    }

    public static double Beta(int height, int width)
    {
        return 1d / width + 1d / height;
    }
}