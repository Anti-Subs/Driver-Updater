// Window layouts (XAML), loaded at run time
namespace DriverUpdater
{
    public static class Xaml
    {
        public const string Main = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""Driver Updater"" Width=""1200"" Height=""780"" MinWidth=""900"" MinHeight=""560""
        WindowStartupLocation=""CenterScreen"" Background=""#F4F6FB""
        FontFamily=""Segoe UI"" FontSize=""13"" TextOptions.TextFormattingMode=""Display"" UseLayoutRounding=""True"">
  <Window.Resources>
    <Style x:Key=""NavButton"" TargetType=""Button"">
      <Setter Property=""Foreground"" Value=""#6B7280""/>
      <Setter Property=""Background"" Value=""Transparent""/>
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""Button"">
            <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" CornerRadius=""12"" Margin=""10,4"" Padding=""0,10"">
              <StackPanel HorizontalAlignment=""Center"">
                <TextBlock Text=""{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}"" FontFamily=""Segoe MDL2 Assets"" FontSize=""22"" HorizontalAlignment=""Center"" Foreground=""{TemplateBinding Foreground}""/>
                <TextBlock Text=""{Binding Content, RelativeSource={RelativeSource TemplatedParent}}"" FontSize=""11"" Margin=""0,6,0,0"" HorizontalAlignment=""Center"" Foreground=""{TemplateBinding Foreground}""/>
              </StackPanel>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#EEF2FF""/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key=""PrimaryButton"" TargetType=""Button"">
      <Setter Property=""Foreground"" Value=""White""/>
      <Setter Property=""FontWeight"" Value=""SemiBold""/>
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""Padding"" Value=""20,9""/>
      <Setter Property=""MinWidth"" Value=""110""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""Button"">
            <Border x:Name=""bd"" Background=""#2563EB"" CornerRadius=""8"" Padding=""{TemplateBinding Padding}"">
              <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#1D4ED7""/></Trigger>
              <Trigger Property=""IsEnabled"" Value=""False""><Setter TargetName=""bd"" Property=""Opacity"" Value=""0.45""/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key=""OutlineButton"" TargetType=""Button"">
      <Setter Property=""Foreground"" Value=""#1F2937""/>
      <Setter Property=""FontWeight"" Value=""SemiBold""/>
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""Padding"" Value=""18,8""/>
      <Setter Property=""MinWidth"" Value=""96""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""Button"">
            <Border x:Name=""bd"" Background=""White"" BorderBrush=""#D1D5DB"" BorderThickness=""1"" CornerRadius=""8"" Padding=""{TemplateBinding Padding}"">
              <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property=""IsMouseOver"" Value=""True"">
                <Setter TargetName=""bd"" Property=""Background"" Value=""#EFF6FF""/>
                <Setter TargetName=""bd"" Property=""BorderBrush"" Value=""#2563EB""/>
              </Trigger>
              <Trigger Property=""IsEnabled"" Value=""False""><Setter TargetName=""bd"" Property=""Opacity"" Value=""0.45""/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key=""SlimProgress"" TargetType=""ProgressBar"">
      <Setter Property=""Height"" Value=""6""/>
      <Setter Property=""Foreground"" Value=""#2563EB""/>
      <Setter Property=""Background"" Value=""#E5E7EB""/>
      <Setter Property=""BorderThickness"" Value=""0""/>
    </Style>

    <Style x:Key=""SectionHeader"" TargetType=""TextBlock"">
      <Setter Property=""FontSize"" Value=""14""/>
      <Setter Property=""FontWeight"" Value=""SemiBold""/>
      <Setter Property=""Foreground"" Value=""#374151""/>
      <Setter Property=""Margin"" Value=""4,18,0,10""/>
    </Style>

    <Style x:Key=""Chip"" TargetType=""Border"">
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""ToolTip"" Value=""Show only these drivers (click again to show all)""/>
      <Setter Property=""Background"" Value=""White""/>
      <Setter Property=""BorderBrush"" Value=""#E5E7EB""/>
      <Setter Property=""BorderThickness"" Value=""1""/>
      <Setter Property=""CornerRadius"" Value=""14""/>
      <Setter Property=""Padding"" Value=""11,4""/>
      <Setter Property=""Margin"" Value=""0,0,8,0""/>
      <Style.Triggers>
        <Trigger Property=""IsMouseOver"" Value=""True""><Setter Property=""BorderBrush"" Value=""#93C5FD""/></Trigger>
      </Style.Triggers>
    </Style>

    <DataTemplate x:Key=""DriverCard"">
      <Border Background=""White"" CornerRadius=""12"" Margin=""0,0,0,8"" BorderBrush=""{Binding CardBorder}"" BorderThickness=""1"" Opacity=""{Binding RowOpacity}"">
        <Grid>
          <Border Width=""4"" HorizontalAlignment=""Left"" Background=""#EF4444"" CornerRadius=""12,0,0,12"" Visibility=""{Binding AccentVisibility}""/>
          <Grid Margin=""18,12,18,12"">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width=""Auto""/>
              <ColumnDefinition Width=""Auto""/>
              <ColumnDefinition Width=""*""/>
              <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>
            <CheckBox IsChecked=""{Binding IsChecked, Mode=TwoWay}"" Visibility=""{Binding CheckVisibility}"" VerticalAlignment=""Center"" Margin=""0,0,14,0""/>
            <Border Grid.Column=""1"" Width=""44"" Height=""44"" CornerRadius=""11"" Background=""{Binding IconBack}"" Margin=""0,0,14,0"">
              <TextBlock Text=""{Binding Glyph}"" FontFamily=""Segoe MDL2 Assets"" FontSize=""19"" Foreground=""{Binding IconFore}"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </Border>
            <StackPanel Grid.Column=""2"" VerticalAlignment=""Center"">
              <TextBlock Text=""{Binding Name}"" FontSize=""14.5"" FontWeight=""SemiBold"" Foreground=""#111827"" TextTrimming=""CharacterEllipsis""/>
              <WrapPanel Margin=""0,4,0,0"">
                <Border Background=""#ECFDF5"" CornerRadius=""6"" Padding=""7,2"" Margin=""0,0,12,0"" Visibility=""{Binding PillVisibility}"">
                  <StackPanel Orientation=""Horizontal"">
                    <TextBlock Text=""&#xE73E;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""10"" Foreground=""#059669"" VerticalAlignment=""Center"" Margin=""0,0,5,0""/>
                    <TextBlock Text=""{Binding Source}"" FontSize=""11.5"" Foreground=""#047857""/>
                  </StackPanel>
                </Border>
                <TextBlock Text=""{Binding VersionLine}"" FontSize=""12"" Foreground=""#6B7280"" Margin=""0,0,14,0"" VerticalAlignment=""Center""/>
                <TextBlock Text=""{Binding Size}"" FontSize=""12"" Foreground=""#9CA3AF"" VerticalAlignment=""Center"" TextWrapping=""Wrap""/>
              </WrapPanel>
            </StackPanel>
            <StackPanel Grid.Column=""3"" Orientation=""Horizontal"" VerticalAlignment=""Center"" Margin=""12,0,0,0"">
              <Border Background=""#EF4444"" CornerRadius=""10"" Padding=""8,3"" Margin=""0,0,10,0"" VerticalAlignment=""Center"" Visibility=""{Binding NewVisibility}"">
                <StackPanel Orientation=""Horizontal"">
                  <TextBlock Text=""&#xE74A;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""10"" Foreground=""White"" VerticalAlignment=""Center"" Margin=""0,0,5,0""/>
                  <TextBlock Text=""Just found"" FontSize=""11"" FontWeight=""SemiBold"" Foreground=""White""/>
                </StackPanel>
              </Border>
              <TextBlock Text=""&#xE72C;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""14"" Foreground=""#2563EB"" VerticalAlignment=""Center"" Margin=""0,0,10,0"" Visibility=""{Binding SpinVisibility}"" RenderTransformOrigin=""0.5,0.5"">
                <TextBlock.RenderTransform><RotateTransform/></TextBlock.RenderTransform>
                <TextBlock.Triggers>
                  <EventTrigger RoutedEvent=""FrameworkElement.Loaded"">
                    <BeginStoryboard>
                      <Storyboard>
                        <DoubleAnimation Storyboard.TargetProperty=""(UIElement.RenderTransform).(RotateTransform.Angle)"" From=""0"" To=""360"" Duration=""0:0:1"" RepeatBehavior=""Forever""/>
                      </Storyboard>
                    </BeginStoryboard>
                  </EventTrigger>
                </TextBlock.Triggers>
              </TextBlock>
              <Border Background=""{Binding TagBack}"" CornerRadius=""12"" Padding=""11,4"" VerticalAlignment=""Center"">
                <TextBlock Text=""{Binding TagText}"" Foreground=""{Binding TagFore}"" FontSize=""12"" FontWeight=""SemiBold""/>
              </Border>
              <Button Content=""{Binding ButtonText}"" Tag=""{Binding ButtonTag}"" IsEnabled=""{Binding ButtonEnabled}"" Visibility=""{Binding ButtonVisibility}"" Style=""{StaticResource OutlineButton}"" Margin=""12,0,0,0""/>
              <Button Content=""Reinstall"" Tag=""Reinstall"" IsEnabled=""{Binding ButtonEnabled}"" Visibility=""{Binding ReinstallVisibility}"" Style=""{StaticResource OutlineButton}"" Margin=""8,0,0,0"" ToolTip=""Install this same driver version again (fixes a driver that stopped working properly)""/>
            </StackPanel>
          </Grid>
        </Grid>
      </Border>
    </DataTemplate>

    <DataTemplate x:Key=""ToolCard"">
      <Border Background=""White"" CornerRadius=""12"" Padding=""18,16"" Margin=""0,0,0,10"" BorderBrush=""#E5E7EB"" BorderThickness=""1"">
        <Grid>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width=""Auto""/>
            <ColumnDefinition Width=""*""/>
            <ColumnDefinition Width=""Auto""/>
          </Grid.ColumnDefinitions>
          <Border Width=""46"" Height=""46"" CornerRadius=""12"" Background=""{Binding IconBack}"" Margin=""0,0,16,0"">
            <TextBlock Text=""{Binding Glyph}"" FontFamily=""Segoe MDL2 Assets"" FontSize=""20"" Foreground=""{Binding IconFore}"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"">
            <StackPanel Orientation=""Horizontal"">
              <TextBlock Text=""{Binding Name}"" FontSize=""14.5"" FontWeight=""SemiBold"" Foreground=""#111827""/>
              <Border Background=""#EFF6FF"" CornerRadius=""6"" Padding=""7,2"" Margin=""10,0,0,0"" Visibility=""{Binding PillVisibility}"">
                <TextBlock Text=""Recommended for your PC"" FontSize=""11"" Foreground=""#1D4ED8"" FontWeight=""SemiBold""/>
              </Border>
            </StackPanel>
            <TextBlock Text=""{Binding Source}"" FontSize=""12"" Foreground=""#6B7280"" Margin=""0,5,0,0"" TextWrapping=""Wrap""/>
          </StackPanel>
          <Button Grid.Column=""2"" Content=""{Binding ButtonText}"" Tag=""Tool"" IsEnabled=""{Binding ButtonEnabled}"" Style=""{StaticResource OutlineButton}"" VerticalAlignment=""Center"" Margin=""12,0,0,0""/>
        </Grid>
      </Border>
    </DataTemplate>
    <!-- Thin modern scrollbar (vertical) and a clear checkbox; both follow light/dark colours -->
    <Style TargetType=""ScrollBar"">
      <Style.Triggers>
        <Trigger Property=""Orientation"" Value=""Vertical"">
          <Setter Property=""Width"" Value=""12""/>
          <Setter Property=""MinWidth"" Value=""12""/>
          <Setter Property=""Template"">
            <Setter.Value>
              <ControlTemplate TargetType=""ScrollBar"">
                <Grid Background=""Transparent"">
                  <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
                    <Track.Thumb>
                      <Thumb>
                        <Thumb.Template>
                          <ControlTemplate TargetType=""Thumb"">
                            <Border x:Name=""th"" CornerRadius=""4"" Background=""#D1D5DB"" Margin=""3,2""/>
                            <ControlTemplate.Triggers>
                              <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""th"" Property=""Background"" Value=""#9CA3AF""/></Trigger>
                              <Trigger Property=""IsDragging"" Value=""True""><Setter TargetName=""th"" Property=""Background"" Value=""#6B7280""/></Trigger>
                            </ControlTemplate.Triggers>
                          </ControlTemplate>
                        </Thumb.Template>
                      </Thumb>
                    </Track.Thumb>
                  </Track>
                </Grid>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Trigger>
      </Style.Triggers>
    </Style>
    <Style TargetType=""CheckBox"">
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""CheckBox"">
            <StackPanel Orientation=""Horizontal"" Background=""Transparent"">
              <Border x:Name=""box"" Width=""18"" Height=""18"" CornerRadius=""5"" BorderThickness=""1.5"" BorderBrush=""#9CA3AF"" Background=""White"" VerticalAlignment=""Center"">
                <TextBlock x:Name=""tick"" Text=""&#xE73E;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""11"" Foreground=""White"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Visibility=""Collapsed""/>
              </Border>
              <ContentPresenter Margin=""8,0,0,0"" VerticalAlignment=""Center""/>
            </StackPanel>
            <ControlTemplate.Triggers>
              <Trigger Property=""IsChecked"" Value=""True"">
                <Setter TargetName=""box"" Property=""Background"" Value=""#2563EB""/>
                <Setter TargetName=""box"" Property=""BorderBrush"" Value=""#2563EB""/>
                <Setter TargetName=""tick"" Property=""Visibility"" Value=""Visible""/>
              </Trigger>
              <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""box"" Property=""BorderBrush"" Value=""#2563EB""/></Trigger>
              <Trigger Property=""IsEnabled"" Value=""False""><Setter Property=""Opacity"" Value=""0.5""/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>

  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width=""96""/>
      <ColumnDefinition Width=""*""/>
    </Grid.ColumnDefinitions>

    <!-- Sidebar -->
    <Border Background=""White"" BorderBrush=""#E5E7EB"" BorderThickness=""0,0,1,0"">
      <DockPanel>
        <Border DockPanel.Dock=""Top"" Height=""22""/>
        <Button x:Name=""NavAbout"" DockPanel.Dock=""Bottom"" Content=""About"" Tag=""&#xE946;"" Style=""{StaticResource NavButton}"" Margin=""0,0,0,16""/>
        <StackPanel>
          <Grid>
            <Button x:Name=""NavDrivers"" Content=""Drivers"" Tag=""&#xE950;"" Style=""{StaticResource NavButton}""/>
            <Border x:Name=""UpdateBadge"" Background=""#EF4444"" CornerRadius=""9"" MinWidth=""18"" Height=""18"" Padding=""5,0"" HorizontalAlignment=""Right"" VerticalAlignment=""Top"" Margin=""0,8,18,0"" Visibility=""Collapsed"">
              <TextBlock x:Name=""UpdateBadgeText"" Text=""0"" Foreground=""White"" FontSize=""11"" FontWeight=""SemiBold"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </Border>
          </Grid>
          <Button x:Name=""NavTools"" Content=""Tools"" Tag=""&#xE90F;"" Style=""{StaticResource NavButton}""/>
        </StackPanel>
      </DockPanel>
    </Border>

    <Grid Grid.Column=""1"">

      <!-- Drivers page (everything happens here) -->
      <Grid x:Name=""PageDrivers"">
        <Grid.RowDefinitions>
          <RowDefinition Height=""Auto""/>
          <RowDefinition Height=""Auto""/>
          <RowDefinition Height=""Auto""/>
          <RowDefinition Height=""*""/>
          <RowDefinition Height=""Auto""/>
        </Grid.RowDefinitions>

        <StackPanel>
        <Border x:Name=""UpdateBanner"" Visibility=""Collapsed"" Background=""#EFF6FF"" BorderBrush=""#BFDBFE"" BorderThickness=""1"" CornerRadius=""10"" Margin=""36,20,36,0"" Padding=""14,10"">
          <DockPanel>
            <StackPanel x:Name=""UpdateBannerButtons"" DockPanel.Dock=""Right"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
              <Button x:Name=""BtnUpdNotes"" Content=""What's new"" Style=""{StaticResource OutlineButton}"" Margin=""0,0,8,0""/>
              <Button x:Name=""BtnUpdLater"" Content=""Later"" Style=""{StaticResource OutlineButton}"" Margin=""0,0,8,0""/>
              <Button x:Name=""BtnUpdNow"" Content=""Update now"" Style=""{StaticResource PrimaryButton}""/>
            </StackPanel>
            <TextBlock Text=""&#xE946;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""16"" Foreground=""#2563EB"" VerticalAlignment=""Center"" Margin=""0,0,10,0""/>
            <TextBlock x:Name=""UpdateBannerText"" Text="""" FontSize=""13.5"" FontWeight=""SemiBold"" Foreground=""#1E3A8A"" VerticalAlignment=""Center"" TextWrapping=""Wrap"" Margin=""0,0,12,0""/>
          </DockPanel>
        </Border>
        <DockPanel Margin=""36,22,36,10"">
          <StackPanel DockPanel.Dock=""Right"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
            <Button x:Name=""BtnScan"" Content=""Scan now"" Style=""{StaticResource PrimaryButton}"" Margin=""0,0,10,0""/>
            <Button x:Name=""BtnUpdateAll"" Content=""Update all"" Style=""{StaticResource PrimaryButton}"" Visibility=""Collapsed""/>
          </StackPanel>
          <StackPanel VerticalAlignment=""Center"">
            <TextBlock FontSize=""24"" FontWeight=""SemiBold"" Foreground=""#111827"" TextTrimming=""CharacterEllipsis"">
              <Run Text=""Your drivers""/><Run x:Name=""CountColon"" Text=""""/><Run x:Name=""CountRun"" Text="""" Foreground=""#DC2626""/>
            </TextBlock>
            <StackPanel Orientation=""Horizontal"" Margin=""1,4,0,0"">
              <TextBlock Text=""&#xE823;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""12"" Foreground=""#6B7280"" VerticalAlignment=""Center"" Margin=""0,0,6,0""/>
              <TextBlock x:Name=""LastScanText"" Text=""Never scanned"" FontSize=""12.5"" Foreground=""#6B7280"" VerticalAlignment=""Center""/>
            </StackPanel>
          </StackPanel>
        </DockPanel>
        </StackPanel>

        <WrapPanel Grid.Row=""1"" Margin=""36,0,36,14"">
          <Border x:Name=""ChipUpd"" Style=""{StaticResource Chip}"" Visibility=""Collapsed"">
            <StackPanel Orientation=""Horizontal""><Ellipse Width=""8"" Height=""8"" Fill=""#EF4444"" Margin=""0,0,7,0"" VerticalAlignment=""Center""/><TextBlock x:Name=""ChipUpdText"" Foreground=""#374151"" FontSize=""12""/></StackPanel>
          </Border>
          <Border x:Name=""ChipOk"" Style=""{StaticResource Chip}"" Visibility=""Collapsed"">
            <StackPanel Orientation=""Horizontal""><Ellipse Width=""8"" Height=""8"" Fill=""#16A34A"" Margin=""0,0,7,0"" VerticalAlignment=""Center""/><TextBlock x:Name=""ChipOkText"" Foreground=""#374151"" FontSize=""12""/></StackPanel>
          </Border>
          <Border x:Name=""ChipMk"" Style=""{StaticResource Chip}"" Visibility=""Collapsed"">
            <StackPanel Orientation=""Horizontal""><Ellipse Width=""8"" Height=""8"" Fill=""#2563EB"" Margin=""0,0,7,0"" VerticalAlignment=""Center""/><TextBlock x:Name=""ChipMkText"" Foreground=""#374151"" FontSize=""12""/></StackPanel>
          </Border>
          <Border x:Name=""ChipUnk"" Style=""{StaticResource Chip}"" Visibility=""Collapsed"">
            <StackPanel Orientation=""Horizontal""><Ellipse Width=""8"" Height=""8"" Fill=""#F59E0B"" Margin=""0,0,7,0"" VerticalAlignment=""Center""/><TextBlock x:Name=""ChipUnkText"" Foreground=""#374151"" FontSize=""12""/></StackPanel>
          </Border>
          <Border x:Name=""ChipTotal"" Style=""{StaticResource Chip}"">
            <TextBlock x:Name=""ChipTotalText"" Text=""Reading drivers..."" Foreground=""#374151"" FontSize=""12""/>
          </Border>
        </WrapPanel>

        <Border x:Name=""ScanBar"" Grid.Row=""2"" Background=""White"" BorderBrush=""#DBEAFE"" BorderThickness=""1"" CornerRadius=""12"" Padding=""16,12"" Margin=""36,0,36,14"" Visibility=""Collapsed"">
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width=""Auto""/>
              <ColumnDefinition Width=""Auto""/>
              <ColumnDefinition Width=""*""/>
              <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>
            <TextBlock Text=""&#xE72C;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""16"" Foreground=""#2563EB"" VerticalAlignment=""Center"" Margin=""0,0,14,0"" RenderTransformOrigin=""0.5,0.5"">
              <TextBlock.RenderTransform><RotateTransform/></TextBlock.RenderTransform>
              <TextBlock.Triggers>
                <EventTrigger RoutedEvent=""FrameworkElement.Loaded"">
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimation Storyboard.TargetProperty=""(UIElement.RenderTransform).(RotateTransform.Angle)"" From=""0"" To=""360"" Duration=""0:0:1"" RepeatBehavior=""Forever""/>
                    </Storyboard>
                  </BeginStoryboard>
                </EventTrigger>
              </TextBlock.Triggers>
            </TextBlock>
            <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"" Margin=""0,0,18,0"" MaxWidth=""420"">
              <TextBlock x:Name=""ScanBarTitle"" Text=""Checking..."" FontWeight=""SemiBold"" Foreground=""#1E3A8A""/>
              <TextBlock x:Name=""ScanBarSub"" Text="" "" FontSize=""12"" Foreground=""#6B7280"" TextTrimming=""CharacterEllipsis""/>
            </StackPanel>
            <ProgressBar x:Name=""ScanProgress"" Grid.Column=""2"" Style=""{StaticResource SlimProgress}"" VerticalAlignment=""Center""/>
            <TextBlock x:Name=""ScanEta"" Grid.Column=""3"" Text="""" FontSize=""12"" Foreground=""#6B7280"" VerticalAlignment=""Center"" Margin=""16,0,0,0""/>
          </Grid>
        </Border>

        <ScrollViewer x:Name=""ListScroll"" Grid.Row=""3"" VerticalScrollBarVisibility=""Auto"" Padding=""36,0,36,12"">
          <ItemsControl x:Name=""ListDrivers"" ItemTemplate=""{StaticResource DriverCard}""/>
        </ScrollViewer>

        <Border Grid.Row=""4"" Background=""White"" BorderBrush=""#E5E7EB"" BorderThickness=""0,1,0,0"" Padding=""36,12"">
          <DockPanel>
            <ProgressBar x:Name=""InstallProgress"" DockPanel.Dock=""Right"" Width=""240"" Style=""{StaticResource SlimProgress}"" Visibility=""Hidden""/>
            <TextBlock x:Name=""FooterStatus"" Text="" "" Foreground=""#6B7280"" VerticalAlignment=""Center"" TextTrimming=""CharacterEllipsis"" Margin=""0,0,16,0""/>
          </DockPanel>
        </Border>
      </Grid>

      <!-- Tools page -->
      <Grid x:Name=""PageTools"" Visibility=""Collapsed"">
        <ScrollViewer VerticalScrollBarVisibility=""Auto"" Padding=""36,30,36,20"">
          <StackPanel>
            <TextBlock Text=""Utilities"" FontSize=""24"" FontWeight=""SemiBold"" Foreground=""#111827"" Margin=""0,0,0,14""/>
            <WrapPanel>
              <Button x:Name=""BtnRestore"" Content=""System Restore"" Style=""{StaticResource OutlineButton}"" Margin=""0,0,10,10""/>
              <Button x:Name=""BtnDevMgr"" Content=""Device Manager"" Style=""{StaticResource OutlineButton}"" Margin=""0,0,10,10""/>
              <Button x:Name=""BtnOldUsb"" Content=""Remove old USB devices..."" Style=""{StaticResource OutlineButton}"" Margin=""0,0,10,10"" ToolTip=""Clears USB devices Windows remembers but that aren't plugged in now""/>
              <Button x:Name=""BtnExport"" Content=""Save driver list..."" Style=""{StaticResource OutlineButton}"" Margin=""0,0,10,10""/>
              <Button x:Name=""BtnHwReport"" Content=""Save hardware report..."" Style=""{StaticResource OutlineButton}"" Margin=""0,0,10,10"" ToolTip=""Your PC's maker, model and devices (no personal data) – share it to get more makers' driver catalogs added""/>
              <Button x:Name=""BtnLog"" Content=""Open log folder"" Style=""{StaticResource OutlineButton}"" Margin=""0,0,10,10""/>
            </WrapPanel>
            <TextBlock Text=""Manufacturer tools"" FontSize=""24"" FontWeight=""SemiBold"" Foreground=""#111827"" Margin=""0,18,0,0""/>
            <TextBlock Text=""Intel, NVIDIA and your PC maker often release drivers before Microsoft does. Their official tools give you those newer versions safely, matched to your exact hardware."" Foreground=""#6B7280"" FontSize=""13.5"" TextWrapping=""Wrap"" Margin=""0,8,0,20"" MaxWidth=""760"" HorizontalAlignment=""Left""/>
            <ItemsControl x:Name=""ListTools"" ItemTemplate=""{StaticResource ToolCard}""/>
          </StackPanel>
        </ScrollViewer>
      </Grid>

      <!-- About page -->
      <Grid x:Name=""PageAbout"" Visibility=""Collapsed"">
        <ScrollViewer VerticalScrollBarVisibility=""Auto"">
        <StackPanel Margin=""36,30,36,24"" MaxWidth=""760"" HorizontalAlignment=""Left"">
          <TextBlock Text=""About Driver Updater"" FontSize=""24"" FontWeight=""SemiBold"" Foreground=""#111827""/>
          <StackPanel Orientation=""Horizontal"" Margin=""0,6,0,22"">
            <TextBlock x:Name=""AboutVersion"" Text=""Version"" Foreground=""#6B7280"" VerticalAlignment=""Center""/>
            <Button x:Name=""BtnCheckApp"" Content=""Check for updates"" Style=""{StaticResource OutlineButton}"" Margin=""14,0,0,0""/>
          </StackPanel>
          <StackPanel Orientation=""Horizontal"" Margin=""0,-8,0,22"">
            <TextBlock Text=""Appearance"" Foreground=""#374151"" FontWeight=""SemiBold"" VerticalAlignment=""Center"" Margin=""0,0,14,0""/>
            <Button x:Name=""BtnThemeAuto"" Content=""Auto (match Windows)"" Style=""{StaticResource OutlineButton}"" Margin=""0,0,8,0""/>
            <Button x:Name=""BtnThemeLight"" Content=""Light"" Style=""{StaticResource OutlineButton}"" Margin=""0,0,8,0""/>
            <Button x:Name=""BtnThemeDark"" Content=""Dark"" Style=""{StaticResource OutlineButton}""/>
          </StackPanel>
          <Border Background=""White"" CornerRadius=""12"" Padding=""22"" BorderBrush=""#E5E7EB"" BorderThickness=""1"">
            <StackPanel>
              <TextBlock Text=""Where drivers come from"" FontWeight=""SemiBold"" FontSize=""14"" Foreground=""#111827""/>
              <TextBlock TextWrapping=""Wrap"" Foreground=""#4B5563"" Margin=""0,6,0,10"" Text=""Only official sources. Manufacturer installers are opened only if Windows confirms their digital signature, and nothing comes from third-party driver sites. Makers' catalogs are used on PCs and parts they're made for.""/>
              <ItemsControl x:Name=""ListSources"" Margin=""0,0,0,16"">
                <ItemsControl.ItemTemplate>
                  <DataTemplate>
                    <Grid Margin=""0,0,0,9"">
                      <Grid.ColumnDefinitions><ColumnDefinition Width=""Auto""/><ColumnDefinition Width=""*""/></Grid.ColumnDefinitions>
                      <Ellipse Width=""6"" Height=""6"" Fill=""#2563EB"" VerticalAlignment=""Top"" Margin=""2,7,10,0""/>
                      <StackPanel Grid.Column=""1"">
                        <StackPanel Orientation=""Horizontal"">
                          <TextBlock Text=""{Binding Name}"" FontWeight=""SemiBold"" Foreground=""#111827""/>
                          <Border Background=""#DCFCE7"" CornerRadius=""6"" Padding=""6,1"" Margin=""8,0,0,0"" Visibility=""{Binding BadgeVisibility}"">
                            <TextBlock Text=""Used on this PC"" FontSize=""11"" Foreground=""#15803D"" FontWeight=""SemiBold""/>
                          </Border>
                        </StackPanel>
                        <TextBlock Text=""{Binding Desc}"" TextWrapping=""Wrap"" Foreground=""#4B5563"" Margin=""0,2,0,0""/>
                      </StackPanel>
                    </Grid>
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
              <TextBlock Text=""Which drivers are listed"" FontWeight=""SemiBold"" FontSize=""14"" Foreground=""#111827""/>
              <TextBlock TextWrapping=""Wrap"" Foreground=""#4B5563"" Margin=""0,6,0,16"" Text=""Drivers from hardware makers (Intel, NVIDIA, Realtek, your PC maker and so on). Generic drivers built into Windows are updated by Windows itself, so they aren't listed.""/>
              <TextBlock Text=""Safety"" FontWeight=""SemiBold"" FontSize=""14"" Foreground=""#111827""/>
              <TextBlock TextWrapping=""Wrap"" Foreground=""#4B5563"" Margin=""0,6,0,16"" Text=""A System Restore point is created before anything is installed. If a driver causes problems, open Tools &gt; System Restore and pick 'Before Driver Updater'. Windows also refuses drivers that are a worse match for your device than the one you have.""/>
              <TextBlock Text=""Uninstalling"" FontWeight=""SemiBold"" FontSize=""14"" Foreground=""#111827""/>
              <TextBlock TextWrapping=""Wrap"" Foreground=""#4B5563"" Margin=""0,6,0,0"" Text=""Settings &gt; Apps &gt; Installed apps &gt; Driver Updater &gt; Uninstall. Drivers you installed stay on the PC.""/>
            </StackPanel>
          </Border>
          <TextBlock Text=""What's new"" FontSize=""18"" FontWeight=""SemiBold"" Foreground=""#111827"" Margin=""0,28,0,12""/>
          <ItemsControl x:Name=""ListChanges"">
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <Border Background=""White"" CornerRadius=""12"" Padding=""20,14"" Margin=""0,0,0,10"" BorderBrush=""#E5E7EB"" BorderThickness=""1"">
                  <StackPanel>
                    <StackPanel Orientation=""Horizontal"">
                      <TextBlock Text=""{Binding Version}"" FontWeight=""SemiBold"" FontSize=""14"" Foreground=""#111827""/>
                      <TextBlock Text=""{Binding Date}"" Foreground=""#9CA3AF"" Margin=""12,0,0,0"" VerticalAlignment=""Center""/>
                      <Border Background=""#EFF6FF"" CornerRadius=""6"" Padding=""7,2"" Margin=""12,0,0,0"" Visibility=""{Binding BadgeVisibility}"">
                        <TextBlock Text=""{Binding Badge}"" FontSize=""11"" FontWeight=""SemiBold"" Foreground=""#1D4ED8""/>
                      </Border>
                    </StackPanel>
                    <TextBlock Text=""{Binding Notes}"" TextWrapping=""Wrap"" Foreground=""#4B5563"" Margin=""0,8,0,0"" LineHeight=""20""/>
                  </StackPanel>
                </Border>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </StackPanel>
        </ScrollViewer>
      </Grid>
    </Grid>
  </Grid>
</Window>";

        public const string Setup = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""Driver Updater Setup"" Width=""620"" Height=""450"" ResizeMode=""NoResize""
        WindowStartupLocation=""CenterScreen"" Background=""White"" FontFamily=""Segoe UI"" FontSize=""13""
        TextOptions.TextFormattingMode=""Display"" UseLayoutRounding=""True"">
  <Window.Resources>
    <Style x:Key=""PrimaryButton"" TargetType=""Button"">
      <Setter Property=""Foreground"" Value=""White""/>
      <Setter Property=""FontWeight"" Value=""SemiBold""/>
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""MinWidth"" Value=""110""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""Button"">
            <Border x:Name=""bd"" Background=""#2563EB"" CornerRadius=""8"" Padding=""20,9"">
              <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#1D4ED7""/></Trigger>
              <Trigger Property=""IsEnabled"" Value=""False""><Setter TargetName=""bd"" Property=""Opacity"" Value=""0.45""/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key=""OutlineButton"" TargetType=""Button"">
      <Setter Property=""Foreground"" Value=""#1F2937""/>
      <Setter Property=""FontWeight"" Value=""SemiBold""/>
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""MinWidth"" Value=""96""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""Button"">
            <Border x:Name=""bd"" Background=""White"" BorderBrush=""#D1D5DB"" BorderThickness=""1"" CornerRadius=""8"" Padding=""16,8"">
              <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property=""IsMouseOver"" Value=""True"">
                <Setter TargetName=""bd"" Property=""Background"" Value=""#EFF6FF""/>
                <Setter TargetName=""bd"" Property=""BorderBrush"" Value=""#2563EB""/>
              </Trigger>
              <Trigger Property=""IsEnabled"" Value=""False""><Setter TargetName=""bd"" Property=""Opacity"" Value=""0.45""/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <!-- Thin modern scrollbar (vertical) and a clear checkbox; both follow light/dark colours -->
    <Style TargetType=""ScrollBar"">
      <Style.Triggers>
        <Trigger Property=""Orientation"" Value=""Vertical"">
          <Setter Property=""Width"" Value=""12""/>
          <Setter Property=""MinWidth"" Value=""12""/>
          <Setter Property=""Template"">
            <Setter.Value>
              <ControlTemplate TargetType=""ScrollBar"">
                <Grid Background=""Transparent"">
                  <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
                    <Track.Thumb>
                      <Thumb>
                        <Thumb.Template>
                          <ControlTemplate TargetType=""Thumb"">
                            <Border x:Name=""th"" CornerRadius=""4"" Background=""#D1D5DB"" Margin=""3,2""/>
                            <ControlTemplate.Triggers>
                              <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""th"" Property=""Background"" Value=""#9CA3AF""/></Trigger>
                              <Trigger Property=""IsDragging"" Value=""True""><Setter TargetName=""th"" Property=""Background"" Value=""#6B7280""/></Trigger>
                            </ControlTemplate.Triggers>
                          </ControlTemplate>
                        </Thumb.Template>
                      </Thumb>
                    </Track.Thumb>
                  </Track>
                </Grid>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Trigger>
      </Style.Triggers>
    </Style>
    <Style TargetType=""CheckBox"">
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""CheckBox"">
            <StackPanel Orientation=""Horizontal"" Background=""Transparent"">
              <Border x:Name=""box"" Width=""18"" Height=""18"" CornerRadius=""5"" BorderThickness=""1.5"" BorderBrush=""#9CA3AF"" Background=""White"" VerticalAlignment=""Center"">
                <TextBlock x:Name=""tick"" Text=""&#xE73E;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""11"" Foreground=""White"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Visibility=""Collapsed""/>
              </Border>
              <ContentPresenter Margin=""8,0,0,0"" VerticalAlignment=""Center""/>
            </StackPanel>
            <ControlTemplate.Triggers>
              <Trigger Property=""IsChecked"" Value=""True"">
                <Setter TargetName=""box"" Property=""Background"" Value=""#2563EB""/>
                <Setter TargetName=""box"" Property=""BorderBrush"" Value=""#2563EB""/>
                <Setter TargetName=""tick"" Property=""Visibility"" Value=""Visible""/>
              </Trigger>
              <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""box"" Property=""BorderBrush"" Value=""#2563EB""/></Trigger>
              <Trigger Property=""IsEnabled"" Value=""False""><Setter Property=""Opacity"" Value=""0.5""/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height=""130""/>
      <RowDefinition Height=""*""/>
      <RowDefinition Height=""Auto""/>
    </Grid.RowDefinitions>

    <Border>
      <Border.Background>
        <LinearGradientBrush StartPoint=""0,0"" EndPoint=""1,1"">
          <GradientStop Color=""#3B82F6"" Offset=""0""/>
          <GradientStop Color=""#1E40AF"" Offset=""1""/>
        </LinearGradientBrush>
      </Border.Background>
      <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"" Margin=""34,0"">
        <Border Width=""64"" Height=""64"" CornerRadius=""16"" Background=""#33FFFFFF"">
          <TextBlock Text=""&#xE896;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""30"" Foreground=""White"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
        </Border>
        <StackPanel Margin=""18,0,0,0"" VerticalAlignment=""Center"">
          <TextBlock Text=""Driver Updater"" FontSize=""26"" FontWeight=""SemiBold"" Foreground=""White""/>
          <TextBlock x:Name=""HeaderSub"" Text=""Setup"" FontSize=""13"" Foreground=""#DBEAFE"" Margin=""0,2,0,0""/>
        </StackPanel>
      </StackPanel>
    </Border>

    <!-- Options -->
    <StackPanel x:Name=""PageOptions"" Grid.Row=""1"" Margin=""34,24,34,0"">
      <TextBlock x:Name=""WelcomeText"" Text=""Keep your drivers up to date using Windows Update, the Microsoft Update Catalog and official manufacturer tools."" TextWrapping=""Wrap"" Foreground=""#374151""/>
      <TextBlock Text=""Install location"" FontWeight=""SemiBold"" Foreground=""#111827"" Margin=""0,20,0,6""/>
      <DockPanel>
        <Button x:Name=""BtnBrowse"" DockPanel.Dock=""Right"" Content=""Browse..."" Style=""{StaticResource OutlineButton}"" Margin=""10,0,0,0""/>
        <Border BorderBrush=""#D1D5DB"" BorderThickness=""1"" CornerRadius=""8"" Padding=""8,0"">
          <TextBox x:Name=""TxtDir"" BorderThickness=""0"" VerticalContentAlignment=""Center"" Height=""34"" FontSize=""13""/>
        </Border>
      </DockPanel>
      <CheckBox x:Name=""ChkDesktop"" Content=""Create a desktop shortcut"" IsChecked=""True"" Margin=""0,18,0,0"" Foreground=""#374151""/>
      <CheckBox x:Name=""ChkLaunch"" Content=""Open Driver Updater when setup finishes"" IsChecked=""True"" Margin=""0,10,0,0"" Foreground=""#374151""/>
    </StackPanel>

    <!-- Progress -->
    <StackPanel x:Name=""PageProgress"" Grid.Row=""1"" Margin=""34,40,34,0"" Visibility=""Collapsed"">
      <TextBlock Text=""Installing..."" FontSize=""18"" FontWeight=""SemiBold"" Foreground=""#111827""/>
      <TextBlock x:Name=""ProgressText"" Text="" "" Foreground=""#6B7280"" Margin=""0,8,0,16""/>
      <ProgressBar x:Name=""Progress"" Height=""6"" Maximum=""5"" Foreground=""#2563EB"" Background=""#E5E7EB"" BorderThickness=""0""/>
    </StackPanel>

    <!-- Done -->
    <StackPanel x:Name=""PageDone"" Grid.Row=""1"" Margin=""34,30,34,0"" Orientation=""Horizontal"" Visibility=""Collapsed"">
      <Border Width=""56"" Height=""56"" CornerRadius=""28"" Background=""#DCFCE7"" VerticalAlignment=""Top"">
        <TextBlock Text=""&#xE73E;"" FontFamily=""Segoe MDL2 Assets"" FontSize=""26"" Foreground=""#16A34A"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
      </Border>
      <StackPanel Margin=""18,4,0,0"" MaxWidth=""460"">
        <TextBlock Text=""Driver Updater is installed"" FontSize=""18"" FontWeight=""SemiBold"" Foreground=""#111827""/>
        <TextBlock Text=""You'll find it in the Start menu. To remove it later, go to Settings &gt; Apps &gt; Installed apps."" TextWrapping=""Wrap"" Foreground=""#6B7280"" Margin=""0,6,0,0""/>
      </StackPanel>
    </StackPanel>

    <Border Grid.Row=""2"" BorderBrush=""#E5E7EB"" BorderThickness=""0,1,0,0"" Padding=""34,14"" Background=""#F9FAFB"">
      <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Right"">
        <Button x:Name=""BtnCancel"" Content=""Cancel"" Style=""{StaticResource OutlineButton}"" Margin=""0,0,10,0""/>
        <Button x:Name=""BtnInstall"" Content=""Install"" Style=""{StaticResource PrimaryButton}""/>
      </StackPanel>
    </Border>
  </Grid>
</Window>";
    }
}
