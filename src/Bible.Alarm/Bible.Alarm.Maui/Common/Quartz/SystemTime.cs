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
 * https://github.com/quartznet/quartznet/blob/master/src/Quartz/SystemTime.cs
 */

#endregion

using System;

namespace Quartz
{
    /// <summary>
    /// A time source for Quartz.NET that returns the current time.
    /// Original idea by Ayende Rahien:
    /// http://ayende.com/Blog/archive/2008/07/07/Dealing-with-time-in-tests.aspx
    /// </summary>
    public static class SystemTime
    {
        /// <summary>
        /// Return current UTC time via <see cref="Func{TResult}" />. Allows easier unit testing.
        /// </summary>
        public static Func<DateTimeOffset> UtcNow = () => DateTimeOffset.UtcNow;

        /// <summary>
        /// Return current time in current time zone via <see cref="Func&lt;T&gt;" />. Allows easier unit testing.
        /// </summary>
        public static Func<DateTimeOffset> Now = () => DateTimeOffset.Now;
    }
}