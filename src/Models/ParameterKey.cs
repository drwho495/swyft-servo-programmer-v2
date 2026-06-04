namespace Swyft.ServoProgrammer.Models;

/// <summary>
/// Stable identifiers for every editable numeric servo parameter.
/// Used to drive the UI and map view-model values back into <see cref="ServoParameters"/>.
/// </summary>
public enum ParameterKey
{
    // PID coefficients
    P,                  // Proportional (firmware: torque proportion)
    I,                  // Integral     (firmware: integral proportion)
    D,                  // Derivative   (firmware: brake proportion)
    IntegralSpeed,
    MinSignal,          // firmware: min pulse width
    MaxSignal,          // firmware: max pulse width
    MiddleSignal,       // firmware: center / median
    DeadZone,           // firmware: sensitivity
    MaxPower            // firmware: max duty cycle
    // Left/Right range are not edited directly; they are derived from Total Range
    // (or forced to 255 in continuous-rotation mode). See MainViewModel.
}
