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
        var omega = (float)(-2.0 * Math.PI / input.Length);
        
        var evenInput = Buffer.forwardSingleBuffers[0];
        var oddInput = Buffer.forwardSingleBuffers[1];

        evenInput[0] = input[0];
        oddInput[0] = input[1];

        if (Complex.IsNaN(evenInput[0]) || Complex.IsNaN(oddInput[0]))
        {
            throw new Exception();
        }

        int phase = 0;

        if (phaseShift)
            phase -= Size / 2;

        oddInput[0] *= Complex.FromPolarCoordinates(1, omega * phase);

        buffer[0] = evenInput[0] + oddInput[0];
        buffer[1] = evenInput[0] - oddInput[0];
        return buffer;
    }
}