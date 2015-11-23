﻿// <copyright file="ConversionSettingsTemplateSelector.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Windows
{
    using System.Windows;
    using System.Windows.Controls;

    public class ConversionSettingsTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DefaultDataTemplate
        {
            get;
            set;
        }

        public DataTemplate WavSettingsDataTemplate
        {
            get;
            set;
        }

        public DataTemplate Mp3SettingsDataTemplate
        {
            get;
            set;
        }

        public DataTemplate OggSettingsDataTemplate
        {
            get;
            set;
        }

        public DataTemplate AacSettingsDataTemplate
        {
            get;
            set;
        }

        public DataTemplate JpgSettingsDataTemplate
        {
            get;
            set;
        }

        public DataTemplate MkvSettingsDataTemplate
        {
            get;
            set;
        }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null)
            {
                return this.DefaultDataTemplate;
            }

            OutputType outputType = (OutputType)item;
           
            switch (outputType)
            {
                case OutputType.Wav:
                    return this.WavSettingsDataTemplate;

                case OutputType.Mp3:
                    return this.Mp3SettingsDataTemplate;

                case OutputType.Ogg:
                    return this.OggSettingsDataTemplate;

                case OutputType.Aac:
                    return this.AacSettingsDataTemplate;

                case OutputType.Mkv:
                    return this.MkvSettingsDataTemplate;

                case OutputType.Jpg:
                    return this.JpgSettingsDataTemplate;
            }

            return this.DefaultDataTemplate;
        }
    }
}
