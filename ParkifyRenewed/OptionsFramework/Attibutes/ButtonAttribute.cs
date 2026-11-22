using System;

namespace ParkifyRenewed.OptionsFramework.Attibutes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ButtonAttribute(string description, string group, string actionClass = null, string actionMethod = null) : AbstractOptionsAttribute(description, group, actionClass, actionMethod)
    {
    }
}