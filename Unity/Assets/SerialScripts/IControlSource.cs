// Common interface implemented by the 3 receivers (CSV, JSON, Binary).
// Thanks to this, NaveController.cs doesn't need to know which protocol
// you're using: it simply asks for "the one on this object".

public interface IControlSource
{
    bool[] Botones { get; }     // [0]=b1, [1]=b2, [2]=b3, [3]=b4
    int Potenciometro { get; }  // 0 a 1023
}