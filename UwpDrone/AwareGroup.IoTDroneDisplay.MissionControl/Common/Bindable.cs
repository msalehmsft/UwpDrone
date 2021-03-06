﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AwareGroup.IoTDroneDisplay.MissionControl.Common
{
    public class Bindable : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property. 
        // The CallerMemberName attribute that is applied to the optional propertyName 
        // parameter causes the property name of the caller to be substituted as an argument. 
        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (1 == 1) //(PropertyChanged != null)
            {
                if (PropertyChanged == null) return;
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }
}
