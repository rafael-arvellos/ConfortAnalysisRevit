using System.Windows;
using System.Windows.Controls.Primitives;

namespace ConfortAnalysis.Views
{
    public partial class ConfigWindow : Window
    {
        public ConfigWindow()
        {
            InitializeComponent();

            // Linkar eventos de Checked e Unchecked
            AdvancedConfigToggle.Checked   += AdvancedConfigToggle_Changed;
            AdvancedConfigToggle.Unchecked += AdvancedConfigToggle_Changed;

            // Atualiza a visibilidade logo ao iniciar
            UpdateAdvancedVisibility();
        }

        private void AdvancedConfigToggle_Changed(object sender, RoutedEventArgs e)
        {
            UpdateAdvancedVisibility();
        }

        private void UpdateAdvancedVisibility()
        {
            bool showAll = AdvancedConfigToggle.IsChecked == true;

            // Painéis que devem aparecer sempre (mesmo sem advanced)
            CustomPeriodPanel.Visibility = Visibility.Visible;
            CustomMeshPanel.Visibility   = Visibility.Visible;

            // Todos os outros painéis “avançados”:
            // Supondo que você tenha nomeado:
            //   CustomCellSizePanel, CustomDatePanel, CustomMeshPanel, etc.

            var advancedPanels = new[]
            {
                CustomCellSizePanel,
                CustomDatePanel,
                CustomMeshPanel
                // adicione aqui qualquer outro painel avançado
            };

            foreach (var pnl in advancedPanels)
            {
                pnl.Visibility = showAll 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }
    }
}
