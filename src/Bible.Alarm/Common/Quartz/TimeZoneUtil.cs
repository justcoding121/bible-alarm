#region License

/*
 * All content copyright Marko Lahma, unless otherwise indicated. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy
 * of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Original location of this file is below.
 * https://github.com/quartznet/quartznet/blob/master/src/Quartz/Util/TimeZoneUtil.cs
 */

#endregion

namespace Quartz.Util;

public static class TimeZoneUtil
{
    private static readonly Dictionary<string, string> TimeZoneIdAliases = new();

    static TimeZoneUtil()
    {
        // Azure has had issues with having both formats
        TimeZoneIdAliases["UTC"] = "Coordinated Universal Time";
        TimeZoneIdAliases["Coordinated Universal Time"] = "UTC";

        // Mono differs in naming too...
        TimeZoneIdAliases["Central European Standard Time"] = "CET";
        TimeZoneIdAliases["CET"] = "Central European Standard Time";

        TimeZoneIdAliases["Eastern Standard Time"] = "US/Eastern";
        TimeZoneIdAliases["US/Eastern"] = "Eastern Standard Time";

        TimeZoneIdAliases["Central Standard Time"] = "US/Central";
        TimeZoneIdAliases["US/Central"] = "Central Standard Time";

        TimeZoneIdAliases["US Central Standard Time"] = "US/Indiana-Stark";
        TimeZoneIdAliases["US/Indiana-Stark"] = "US Central Standard Time";

        TimeZoneIdAliases["Mountain Standard Time"] = "US/Mountain";
        TimeZoneIdAliases["US/Mountain"] = "Mountain Standard Time";

        TimeZoneIdAliases["US Mountain Standard Time"] = "US/Arizona";
        TimeZoneIdAliases["US/Arizona"] = "US Mountain Standard Time";

        TimeZoneIdAliases["Pacific Standard Time"] = "US/Pacific";
        TimeZoneIdAliases["US/Pacific"] = "Pacific Standard Time";

        TimeZoneIdAliases["Alaskan Standard Time"] = "US/Alaska";
        TimeZoneIdAliases["US/Alaska"] = "Alaskan Standard Time";

        TimeZoneIdAliases["Hawaiian Standard Time"] = "US/Hawaii";
        TimeZoneIdAliases["US/Hawaii"] = "Hawaiian Standard Time";

        TimeZoneIdAliases["China Standard Time"] = "Asia/Beijing";
        TimeZoneIdAliases["Asia/Shanghai"] = "China Standard Time";
        TimeZoneIdAliases["Asia/Beijing"] = "China Standard Time";

        TimeZoneIdAliases["Pakistan Standard Time"] = "Asia/Karachi";
        TimeZoneIdAliases["Asia/Karachi"] = "Pakistan Standard Time";
    }

    public static Func<string, TimeZoneInfo> CustomResolver = id => null;

    /// <summary>
    /// TimeZoneInfo.ConvertTime is not supported under mono
    /// </summary>
    /// <param name="dateTimeOffset"></param>
    /// <param name="timeZoneInfo"></param>
    /// <returns></returns>
    public static DateTimeOffset ConvertTime(DateTimeOffset dateTimeOffset, TimeZoneInfo timeZoneInfo)
    {
        if (QuartzEnvironment.IsRunningOnMono)
            return TimeZoneInfo.ConvertTime(dateTimeOffset.UtcDateTime, TimeZoneInfo.Utc, timeZoneInfo);

        return TimeZoneInfo.ConvertTime(dateTimeOffset, timeZoneInfo);
    }

    /// <summary>
    /// TimeZoneInfo.GetUtcOffset(DateTimeOffset) is not supported under mono
    /// </summary>
    /// <param name="dateTimeOffset"></param>
    /// <param name="timeZoneInfo"></param>
    /// <returns></returns>
    public static TimeSpan GetUtcOffset(DateTimeOffset dateTimeOffset, TimeZoneInfo timeZoneInfo)
    {
        if (QuartzEnvironment.IsRunningOnMono) return timeZoneInfo.GetUtcOffset(dateTimeOffset.UtcDateTime);

        return timeZoneInfo.GetUtcOffset(dateTimeOffset);
    }

    public static TimeSpan GetUtcOffset(DateTime dateTime, TimeZoneInfo timeZoneInfo)
    {
        // Unlike the default behavior of TimeZoneInfo.GetUtcOffset, it is prefered to choose
        // the DAYLIGHT time when the input is ambiguous, because the daylight instance is the
        // FIRST instance, and time moves in a forward direction.

        var offset = timeZoneInfo.IsAmbiguousTime(dateTime)
            ? timeZoneInfo.GetAmbiguousTimeOffsets(dateTime).Max()
            : timeZoneInfo.GetUtcOffset(dateTime);

        return offset;
    }

    /// <summary>
    /// Tries to find time zone with given id, has ability do some fallbacks when necessary.
    /// </summary>
    /// <param name="id">System id of the time zone.</param>
    /// <returns></returns>
    public static TimeZoneInfo FindTimeZoneById(string id)
    {
        TimeZoneInfo info = null;
        try
        {
            info = TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException ex)
        {
            if (TimeZoneIdAliases.TryGetValue(id, out var aliasedId))
                try
                {
                    info = TimeZoneInfo.FindSystemTimeZoneById(aliasedId);
                }
                catch
                {
                }

            if (info == null) info = CustomResolver(id);

            if (info == null)
                // we tried our best
                throw new TimeZoneNotFoundException(
                    $"Could not find time zone with id {id}, consider using Quartz.Plugins.TimeZoneConverter for resolving more time zones ids",
                    ex);
        }

        return info;
    }
}