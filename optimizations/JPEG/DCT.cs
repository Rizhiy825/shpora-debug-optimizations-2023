using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JPEG.Utilities;

namespace JPEG;

public class DCT
{
	public static void DCT2D(double[,] input, double[,] outputCoeffs)
	{
		var height = input.GetLength(0);
		var width = input.GetLength(1);
		
		Parallel.For(0, height, SumVars);
		
		void SumVars(int u)
		{
			for (int v = 0; v < height; v++)
			{
				var sum = MathEx
					.SumByTwoVariables(
						0, width,
						0, height,
						(x, y) => BasisFunction(input[x, y], u, v, x, y, height, width));

				outputCoeffs[u, v] = sum * Beta(height, width) * Alpha(u) * Alpha(v);
			
			}
		}
		
		#region MyRegion

		//MathEx.LoopByTwoVariables(
		//    0, width,
		//    0, height,
		//    (u, v) =>
		//    {
		//        var sum = MathEx
		//            .SumByTwoVariables(
		//                0, width,
		//                0, height,
		//                (x, y) => BasisFunction(input[x, y], u, v, x, y, height, width));

		//        outputCoeffs[u, v] = sum * Beta(height, width) * Alpha(u) * Alpha(v);
		//    });

		#endregion
	}

	public static void IDCT2D(double[,] coeffs, double[,] output)
    {
        Parallel.For(0, coeffs.GetLength(1), SumVars);

        void SumVars(int x)
        {
            for (var y = 0; y < coeffs.GetLength(0); y++)
            {
                var sum = MathEx
                    .SumByTwoVariables(
                        0, coeffs.GetLength(1),
                        0, coeffs.GetLength(0),
                        (u, v) =>
                            BasisFunction(coeffs[u, v], u, v, x, y, coeffs.GetLength(0), coeffs.GetLength(1)) *
                            Alpha(u) * Alpha(v));

                output[x, y] = sum * Beta(coeffs.GetLength(0), coeffs.GetLength(1));
            }
        }
    }

    public static double BasisFunction(double a, double u, double v, double x, double y, int height, int width)
	{
		var b = Math.Cos(((2d * x + 1d) * u * Math.PI) / (2 * width));
		var c = Math.Cos(((2d * y + 1d) * v * Math.PI) / (2 * height));

		return a * b * c;
	}

	private static double Alpha(int u)
	{
		if (u == 0)
			return 1 / Math.Sqrt(2);
		return 1;
	}

	private static double Beta(int height, int width)
	{
		return 1d / width + 1d / height;
	}
}