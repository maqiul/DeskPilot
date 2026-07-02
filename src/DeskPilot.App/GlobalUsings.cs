// v0.18.0: 全局 using 别名 - 解决 <UseWindowsForms>true</UseWindowsForms> 引入的命名空间冲突
// 优先解析 WPF 类型
global using Application = System.Windows.Application;
global using Brushes = System.Windows.Media.Brushes;
global using Button = System.Windows.Controls.Button;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MessageBox = System.Windows.MessageBox;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxImage = System.Windows.MessageBoxImage;
global using MessageBoxResult = System.Windows.MessageBoxResult;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using Binding = System.Windows.Data.Binding;