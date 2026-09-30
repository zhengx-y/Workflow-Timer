using System;
using System.Windows.Input;
using Workflow_Timer.Helpers;

namespace Workflow_Timer.Commands
{
    public class PresetCommands
    {
        public ICommand LoadPresetCommand { get; }

        public ICommand SavePresetCommand { get; }

        public ICommand CreatePresetCommand { get; }

        public ICommand DeletePresetCommand { get; }



        public PresetCommands(
            Action load,
            Action save,
            Action create,
            Action delete)
        {
            LoadPresetCommand =
                new RelayCommand(load);

            SavePresetCommand =
                new RelayCommand(save);

            CreatePresetCommand =
                new RelayCommand(create);

            DeletePresetCommand =
                new RelayCommand(delete);
        }
    }
}