﻿using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MonlistClone.Data;

namespace MonlistClone.Util {
  public class DayTypeColorConverter :IValueConverter{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      DayType dt = (DayType) value;
      Brush b = Brushes.Transparent;
      switch (dt) {
        case DayType.Unknown:
        case DayType.Working:
          b = Brushes.DarkGray;
          break;
        case DayType.Weekend:
          b = Brushes.LightGreen;
          break;
        case DayType.Holiday:
          b = Brushes.LightSkyBlue;
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
      return b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
    }
  }
}