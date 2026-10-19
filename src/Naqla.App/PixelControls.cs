using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Naqla.App;

internal static class PixelControls
{
    public static void Apply(ResourceDictionary resources)
    {
        resources[typeof(ComboBox)] = XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ComboBox">
             <Setter Property="Foreground" Value="#F5F0E6"/><Setter Property="Background" Value="#1C2530"/><Setter Property="MinHeight" Value="34"/>
             <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid>
              <ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="#1C2530" BorderBrush="#415266" BorderThickness="1"/></ControlTemplate></ToggleButton.Template></ToggleButton>
              <ContentPresenter IsHitTestVisible="False" Margin="10,7,26,7" Content="{TemplateBinding SelectionBoxItem}"/><TextBlock Text="▾" IsHitTestVisible="False" HorizontalAlignment="Right" Margin="0,6,8,0" Foreground="#FFC35A"/>
              <Popup IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True"><Border Background="#1C2530" BorderBrush="#FFC35A" BorderThickness="1" MinWidth="{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}"><ScrollViewer MaxHeight="250"><ItemsPresenter/></ScrollViewer></Border></Popup>
             </Grid></ControlTemplate></Setter.Value></Setter>
            </Style>
            """);
        resources[typeof(Slider)] = XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Slider"><Setter Property="MinHeight" Value="25"/>
             <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider"><Grid><Border Background="#344454" Height="4"/>
              <Track x:Name="PART_Track" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Orientation="Horizontal">
               <Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton>
               <Track.Thumb><Thumb Width="12" Height="18"><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#FFC35A" BorderBrush="#FFE4AF" BorderThickness="1"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
               <Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton>
              </Track></Grid></ControlTemplate></Setter.Value></Setter>
            </Style>
            """);
    }
}
