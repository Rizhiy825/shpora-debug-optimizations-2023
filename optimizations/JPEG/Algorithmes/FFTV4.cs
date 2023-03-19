using System.Numerics;
using JPEG.Utilities;

namespace JPEG.Algorithmes;

public class FFTV4
{
    public int Size { get; set; }

    public FFTV4(int size)
    {
        Size = size;
    }


    public Complex[] Forward(Complex[] input, Complex[] buffer, Complex polarComplex)
    {
        input[1] *= polarComplex;
        buffer[0] = input[0] + input[1];
        buffer[1] = input[0] - input[1];
        return buffer;
    }
}