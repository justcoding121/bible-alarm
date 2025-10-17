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
 * Removed methods not useful in this project from original file located in below url.
 * https://github.com/quartznet/quartznet/blob/master/src/Quartz/Util/QuartzEnvironment.cs
 */

#endregion
using System;

namespace Quartz.Util
{
    /// <summary>
    /// Environment access helpers that fail gracefully if under medium trust.
    /// </summary>
    public static class QuartzEnvironment
    {

        /// <summary>
        /// Return whether we are currently running under Mono runtime.
        /// </summary>
        public static bool IsRunningOnMono { get; } = Type.GetType("Mono.Runtime") != null;

    }
}