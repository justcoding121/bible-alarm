// Decompiled with JetBrains decompiler
// Type: Redux.Middleware`1
// Assembly: Redux, Version=1.0.1.0, Culture=neutral, PublicKeyToken=null
// MVID: C5F64108-560B-4DF4-8351-166C386E1779
// Assembly location: C:\Work\Repositories\Bible-Alarm\src\Bible.Alarm\Bible.Alarm.UWP\bin\x86\Debug\Redux.dll

using System;

namespace Redux
{
    public delegate Func<Dispatcher, Dispatcher> Middleware<TState>(
    IStore<TState> store);
}
