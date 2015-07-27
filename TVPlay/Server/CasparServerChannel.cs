﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Svt.Caspar;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using TAS.Common;

namespace TAS.Server
{
    public class CasparServerChannel : PlayoutServerChannel
    {
        private Channel _casparChannel;
        internal Channel CasparChannel
        {
            set
            {
                if (_casparChannel != value)
                    _casparChannel = value;
            }
        }

        public string LiveDevice { get; set; }

        private bool _checkConnected()
        {
            var server = OwnerServer;
            var channel = _casparChannel;
            if (server != null && channel != null)
                return server.IsConnected;
            return false;
        }

        protected override TVideoFormat _getFormat()
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
                switch (channel.VideoMode)
                {
                    case VideoMode.PAL:
                        return TVideoFormat.PAL_FHA;
                    case VideoMode.NTSC:
                        return TVideoFormat.NTSC;
                    case VideoMode.HD720p5000:
                        return TVideoFormat.HD720p5000;
                    case VideoMode.HD1080i5000:
                        return TVideoFormat.HD1080i5000;
                    default:
                        return TVideoFormat.Other;
                }
            return TVideoFormat.Other;
        }


        internal override void Initialize()
        {
            lock (this)
            {
                var channel = _casparChannel;
                if (channel != null
                    && OwnerServer != null
                    && OwnerServer.IsConnected)
                    channel.CustomCommand(string.Format(CultureInfo.InvariantCulture, "MIXER {0} MASTERVOLUME {1:F3}", ChannelNumber, MasterVolume));
            }
        }

        public override event VolumeChangeNotifier OnVolumeChanged;

        private CasparItem _getItem(Event aEvent)
        {
            CasparItem item = new CasparItem(string.Empty);
            ServerMedia media = (aEvent.Engine.PlayoutChannelPGM == this) ? aEvent.ServerMediaPGM : aEvent.ServerMediaPRV;
            if (aEvent.EventType == TEventType.Live || media != null)
            {
                if (aEvent.EventType == TEventType.Movie || aEvent.EventType == TEventType.StillImage)
                {
                    item.Clipname = "\"" + Path.GetFileNameWithoutExtension(media.FileName) + "\"" +
                        ((media.MediaType == TMediaType.Movie && media.HasExtraLines) ? " FILTER CROP=720:576:0:32" : string.Empty) +
                        (media.MediaType == TMediaType.Movie ? " CHANNEL_LAYOUT STEREO" : string.Empty);
                }
                if (aEvent.EventType == TEventType.Live)
                    item.Clipname = LiveDevice ?? "BLACK";
                item.VideoLayer = (int)aEvent.Layer;
                item.Loop = false;
                item.Transition.Duration = (int)(aEvent.TransitionTime.Ticks / Engine.FrameTicks);
                item.Seek = (int)aEvent.SeekPGM;
                item.Transition.Type = (Svt.Caspar.TransitionType)aEvent.TransitionType;
                return item;
            }
            else
                return null;
        }

        private CasparItem _getItem(ServerMedia media, VideoLayer videolayer, long seek)
        {
            if (media != null)
            {
                CasparItem item = new CasparItem(string.Empty);
                if (media.MediaType == TMediaType.Movie || media.MediaType == TMediaType.Movie)
                    item.Clipname = "\"" + Path.GetFileNameWithoutExtension(media.FileName) + "\"" +
                        ((media.MediaType == TMediaType.Movie && media.HasExtraLines) ? " FILTER CROP=720:576:0:32" : string.Empty) +
                        ((media.MediaType == TMediaType.Movie) ? " CHANNEL_LAYOUT STEREO" : string.Empty);
                item.VideoLayer = (int)videolayer;
                item.Seek = (int)seek;
                return item;
            }
            else
                return null;
        }

        private CasparCGDataCollection GetContainerData(Template template)
        {
            var data =  new CasparCGDataCollection();
            foreach (var field in template.TemplateFields)
                data.DataPairs.Add(new CGDataPair(field.Key, new CGTextFieldData(field.Value)));
            return data;
        }

        public override bool LoadNext(Event aEvent)
        {
            var channel = _casparChannel;
            if (aEvent != null && _checkConnected() && channel != null)
            {
                if (aEvent.EventType == TEventType.Live || aEvent.EventType == TEventType.Movie || aEvent.EventType == TEventType.StillImage)
                {
                    CasparItem item = _getItem(aEvent);
                    if (item != null)
                    {
                        channel.LoadBG(item);
                        Debug.WriteLine(aEvent, "CasparLoadNext: ");
                        return true;
                    }
                }
                if (aEvent.EventType == TEventType.AnimationFlash)
                {
                    var template = aEvent.Template;
                    var media = aEvent.Media;
                    if (template != null && media != null)
                    {
                        channel.CG.Add((int)aEvent.Layer, template.Layer, media.FileName, false, GetContainerData(template));
                    }
                }
            }
            Debug.WriteLine(aEvent, "LoadNext did not load: ");
            return false;
        }

        public override bool Load(Event aEvent)
        {
            var channel = _casparChannel;
            if (aEvent != null && channel != null && _checkConnected())
            {
                if (aEvent.EventType == TEventType.Live || aEvent.EventType == TEventType.Movie || aEvent.EventType == TEventType.StillImage)
                {
                    CasparItem item = _getItem(aEvent);
                    if (item != null)
                    {
                        channel.Load(item);
                        Debug.WriteLine(aEvent, "CasparLoad: ");
                        Media m = aEvent.Media;
                        return true;
                    }
                }
                if (aEvent.EventType == TEventType.AnimationFlash)
                {
                    var template = aEvent.Template;
                    var media = aEvent.Media;
                    if (template != null && media != null)
                    {
                        channel.CG.Add((int)aEvent.Layer, template.Layer, media.FileName, false, GetContainerData(template));
                    }
                }
            }
            Debug.WriteLine(aEvent, "CasparLoad did not load: ");
            return false;
        }

        public override bool Load(ServerMedia media, VideoLayer videolayer, long seek, long duration)
        {
            var channel = _casparChannel;
            if (_checkConnected() 
                && media != null 
                && channel != null)
            {
                CasparItem item = _getItem(media, videolayer, seek);
                if (item != null)
                {
                    item.Length = (int)duration;
                    channel.Load(item);
                    Debug.WriteLine("CasparLoad media {0} Layer {1} Seek {2}", media, videolayer, seek);
                    return true;
                }
            }
            return false;
        }

        public override bool Load(System.Drawing.Color color, VideoLayer videolayer)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                var scolor = '#' + color.ToArgb().ToString("X8");
                CasparItem item = new CasparItem((int)videolayer, scolor);
                channel.Load(item);
                Debug.WriteLine("CasparLoad color {0} Layer {1}", scolor, videolayer);
                return true;
            }
            return false;
        }
        

        public override bool Seek(VideoLayer videolayer, long position)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                channel.CustomCommand(string.Format("CALL {0}-{1} SEEK {2}", ChannelNumber, (int)videolayer, position));
                Debug.WriteLine("CasparSeek Channel {0} Layer {1} Position {2}", ChannelNumber, (int)videolayer, position);
                return true;
            }
            return false;
        }

        public override bool Play(Event aEvent)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                if (aEvent.EventType != TEventType.AnimationFlash)
                {
                    Media m = aEvent.Media;
                    if (aEvent.EventType == TEventType.Live || m != null)
                        channel.Play((int)aEvent.Layer);
                }
                else
                    channel.CG.Play((int)aEvent.Layer);
                Debug.WriteLine(aEvent, string.Format("CasparPlay Layer {0}", aEvent.Layer));
                return true;
            }
            return false;
        }

        public override bool Play(VideoLayer videolayer)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                {
                    channel.Play((int)videolayer);
                    Debug.WriteLine("CasparPlay Layer {0}", videolayer);
                    return true;
                }
            }
            return false;
        }

        public override bool Stop(Event aEvent)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                {
                    channel.Stop((int)aEvent.Layer);
                    Debug.WriteLine(aEvent, string.Format("CasprarStop {0} layer {1}", aEvent, aEvent.Layer));
                }
                return true;
            }
            else
                return false;
        }

        public override bool Stop(VideoLayer videolayer)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                {
                    channel.Stop((int)videolayer);
                    Debug.WriteLine("CasparStop Layer {0}", videolayer);
                    return true;
                }
            }
            return false;
        }

        public override bool Pause(Event aEvent)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                {
                    channel.CustomCommand(string.Format("PAUSE {0}-{1}", ChannelNumber, (int)aEvent.Layer));
                    Debug.WriteLine(aEvent, string.Format("CasprarPause {0} layer {1}", aEvent, aEvent.Layer));
                }
                return true;
            }
            else
                return false;
        }

        public override bool Pause(VideoLayer videolayer)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                {
                    channel.CustomCommand(string.Format("PAUSE {0}-{1}", ChannelNumber, (int)videolayer));
                    Debug.WriteLine("CasparPause Layer {0}", videolayer);
                    return true;
                }
            }
            return false;
        }


        public override void ReStart(VideoLayer aVideoLayer)
        {
            Event ev = Engine._visibleEvents[aVideoLayer];
            var channel = _casparChannel;
            if (_checkConnected()
                && ev != null
                && channel != null)
            {
                CasparItem item = _getItem(ev);
                if (item != null)
                {
                    if (ev.EventType == TEventType.Movie && ev.Media != null)
                        item.Seek = (int)ev.Position + (int)((ev.ScheduledTC.Ticks - ev.Media.TCPlay.Ticks) / Engine.FrameTicks);
                    item.Transition.Duration = 3;
                    item.Transition.Type = TransitionType.MIX;
                    channel.LoadBG(item);
                    channel.Play(item.VideoLayer);
                    Debug.WriteLine("CasparChanner.ReStart: restarted {0} from frame {1}", item.Clipname, item.Seek);
                }
                Event le;
                Engine._loadedNextEvents.TryRemove(aVideoLayer, out le);
                if (le != null)
                {
                    LoadNext(le); // workaround to reload event removed with CasarChanenel.Stop()
                    Debug.WriteLine("CasparChanner.ReStart: reloaded {0}", le.ToString());
                }
            }
        }

        public override void Clear(VideoLayer aVideoLayer)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                channel.Clear((int)aVideoLayer);
                Debug.WriteLine(aVideoLayer, "CasparClear");
            }
        }
        public override void Clear()
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                channel.Clear();
                Debug.WriteLine(this, "CasparClear");
            }
        }

        public override void SetVolume(VideoLayer videolayer, decimal volume)
        {
            var channel = _casparChannel;
            if (_checkConnected() && channel != null)
            {
                channel.CustomCommand(string.Format(CultureInfo.InvariantCulture, "MIXER {0}-{1} VOLUME {2:F3}", ChannelNumber, (int)videolayer, volume));
                if (OnVolumeChanged != null)
                    OnVolumeChanged(this, videolayer, volume);
            }
        }

        public override void SetAspect(bool narrow)
        {
            var channel = _casparChannel;
            var oldAspectNarrow = outputAspectNarrow;
            if (oldAspectNarrow != narrow
                && channel != null
                && _checkConnected())
            {
                outputAspectNarrow = narrow;
                if (narrow)
                    channel.CustomCommand(string.Format("MIXER {0}-{1} FILL 0.125 0 0.75 1 10", ChannelNumber, (int)VideoLayer.Program));
                else
                    channel.CustomCommand(string.Format("MIXER {0}-{1} FILL 0 0 1 1 10", ChannelNumber, (int)VideoLayer.Program));
                Debug.WriteLine("SetAspect narrow: {0}", narrow);
            }
        }

    }
}