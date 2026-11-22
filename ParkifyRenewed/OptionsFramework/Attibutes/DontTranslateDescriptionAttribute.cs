using System;
using System.ComponentModel;

namespace ParkifyRenewed.OptionsFramework.Attibutes
{
    [AttributeUsage(AttributeTargets.All)]
    public class DontTranslateDescriptionAttribute(string description) : DescriptionAttribute(description)
    {
    }
}