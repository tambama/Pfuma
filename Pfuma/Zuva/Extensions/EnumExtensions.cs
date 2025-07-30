using System;
using System.ComponentModel;
using System.Reflection;
using cAlgo.API;
using Zuva.Models;

namespace Zuva.Extensions;

public static class EnumExtensions
{
    public static (Color color, LineStyle style) GetLineStyle(this LineType lineType)
    {
        return lineType switch
        {
            LineType.IND => (Color.Wheat, LineStyle.Dots),
            LineType.BOS => (Color.Wheat, LineStyle.Dots),
            LineType.CHOCH => (Color.Red, LineStyle.Solid),
            LineType.CISD => (Color.Pink, LineStyle.Solid),
            LineType.Unicorn => (Color.Red, LineStyle.Solid),
            LineType.OF => (Color.Aqua, LineStyle.Dots),
            _ => (Color.Gray, LineStyle.Solid)
        };
    }
    
    /// <summary>
    /// Gets the description attribute from an enum value
    /// </summary>
    public static string GetDescription(this Enum value)
    {
        FieldInfo field = value.GetType().GetField(value.ToString());
        
        if (field == null) 
            return value.ToString();
            
        var attributes = (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), false);
        
        return attributes.Length > 0 ? attributes[0].Description : value.ToString();
    }
}