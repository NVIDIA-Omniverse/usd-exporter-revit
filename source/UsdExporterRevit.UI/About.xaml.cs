// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using UsdExporterRevitSdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Navigation;

namespace UsdExporterRevit.UI
{
/// <summary>
/// Interaction logic for About.xaml
/// </summary>
public partial class About : Window
{
    public About(IntPtr windowHandle)
    {
        // Displayed in the center of the screen.
        HwndSource hwndSource = HwndSource.FromHwnd(windowHandle);
        Window wnd = hwndSource.RootVisual as Window;
        this.Owner = wnd;
        this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        InitializeComponent();
        populateUI();
    }

    private void populateUI()
    {
        Assembly a = Assembly.GetExecutingAssembly();
        T GetCustomAttribute<T>() where T : Attribute => (T)Attribute.GetCustomAttribute(a, typeof(T));

        Version version = a.GetName().Version;
        text_title.Text = "USD Exporter for Revit";
        text_build_version.Text = $"Build: {version.Major}.{version.Minor}.{version.Build}";

        var attr = GetCustomAttribute<AssemblyCopyrightAttribute>();
        if (attr != null)
        {
            text_copyright.Text = $"{attr.Copyright}";
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        ProcessStartInfo pi = new ProcessStartInfo() {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true,
        };
        Process.Start(pi);
    }
}
}
