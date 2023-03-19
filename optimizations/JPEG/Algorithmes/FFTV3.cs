using System.Numerics;
using System;
using JPEG.Images;
using Buffer = JPEG.Utilities.Buffer;

namespace JPEG.Algorithmes;

public class FFTV3
{
    public int Size { get; set; }

    public FFTV3(int size)
    {
        Size = size;
    }

    public Complex[] Forward(Complex[] input, Complex[] buffer, bool phaseShift = true)
    {
        int phase = 0;
        if (phaseShift) phase -= Size / 2;

        input[1] *= Complex.FromPolarCoordinates(1, Buffer.Omega * phase);

        buffer[0] = input[0] + input[1];
        buffer[1] = input[0] - input[1];
        return buffer;
    }
}