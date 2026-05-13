#nullable enable

using System;
using System.Globalization;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Resolves the parameter passed to an <c>ICommand</c> from explicit command parameter, event args, and an optional converter.
/// </summary>
public static class EventToCommandParameterResolver
{
    public static object? Resolve(
        object? commandParameter,
        EventArgs? eventArgs,
        Func<object, object?, CultureInfo, object?>? convertEventArgs,
        object? converterParameter,
        CultureInfo culture)
    {
        var parameter = commandParameter;

        if (parameter == null && eventArgs != null && eventArgs != EventArgs.Empty)
        {
            parameter = eventArgs;

            if (convertEventArgs != null)
            {
                parameter = convertEventArgs(eventArgs, converterParameter, culture);
            }
        }

        return parameter;
    }
}
