﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using TAS.Common;
using TAS.Server.Interfaces;

namespace TAS.Client.Model
{
    class ConvertOperation : FileOperation, IConvertOperation
    {
        public TAspectConversion AspectConversion { get { return Get<TAspectConversion>(); } set { Set(value); } }
        public TAudioChannelMappingConversion AudioChannelMappingConversion { get { return Get<TAudioChannelMappingConversion>(); } set { Set(value); } }
        public decimal AudioVolume { get { return Get<decimal>(); } set { Set(value); } }
        public TVideoFormat OutputFormat { get { return Get<TVideoFormat>(); } set { Set(value); } }
        public TFieldOrder SourceFieldOrderEnforceConversion { get { return Get<TFieldOrder>(); } set { Set(value); } }
    }
}