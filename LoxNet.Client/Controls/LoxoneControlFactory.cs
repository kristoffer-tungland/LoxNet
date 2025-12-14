namespace LoxNet;

/// <summary>
/// Factory used to instantiate typed <see cref="LoxoneControl"/> instances.
/// </summary>
public static class LoxoneControlFactory
{
    /// <summary>
    /// Creates a control instance for the given type.
    /// </summary>
    public static LoxoneControl Create(ControlType type) => type switch
    {
        ControlType.Switch => new SwitchControl(),
        ControlType.LightController => new LightController(),
        ControlType.LightControllerV2 => new LightControllerV2(),
        ControlType.Dimmer => new LoxoneControl(),
        ControlType.ColorPickerV2 => new LoxoneControl(),
        _ => new LoxoneControl()
    };

}
