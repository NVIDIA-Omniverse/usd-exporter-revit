// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using UsdExporterRevitSdk;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

using System.Threading;

namespace UsdExporterRevit.UI
{
/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class Progress : Window
{
    private readonly int nobatchHeight = 170;
    private readonly int fullHeight = 260;
    private event PropertyChangedEventHandler PropertyChanged;
    private ProgressContext _context;
    private readonly Object _lock = new Object();

    public ProgressContext Context
    {
        get {
            lock (_lock)
            {
                return _context;
            }
        }
        set {
            lock (_lock)
            {
                _context = value;
                if (this.IsLoaded)
                {
                    OnPropertyChanged();
                }
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // indidcates the dialog was canceled
    private bool cleanclose = false;

    public Progress(ProgressContext context)
    {
        Context = context;
        InitializeComponent();
        initializeContext();
        this.Closing += Progress_Closing;
        PropertyChanged += Progress_PropertyChanged;
    }

    private void Progress_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        initializeContext();
    }

    private void Progress_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!cleanclose && Context.State != ProgressContextState.Complete)
        {
            Context.State = ProgressContextState.Cancelled;
        }
    }

    private void initializeContext()
    {
        switch (Context.State)
        {
            case ProgressContextState.Standard:
                this.Height = nobatchHeight;
                this.row_batch.Height = new GridLength(0);
                tb_view.Text = $"View ({Context.ActiveViewNumber} of {Context.TotalViewNumber}): {Context.ActiveView} - {Context.DisplayMessage}";
                pb_view.Value = Context.ViewProgress;
                break;
            case ProgressContextState.Batch:
                this.Height = fullHeight;
                this.row_batch.Height = new GridLength(65);
                this.row_view.Height = new GridLength(65);

                tb_batch.Text = $"Model ({Context.ActiveModelNumber} of {Context.TotalModelNumber}):{Context.ActiveModel}";
                double batchValue = (Context.ActiveModelNumber * 100.0) / (Context.TotalModelNumber * 1.0);
                pb_batch.Value = batchValue;

                tb_view.Text = $"View ({Context.ActiveViewNumber} of {Context.TotalViewNumber}): {Context.ActiveView} - {Context.DisplayMessage}";
                pb_view.Value = Context.ViewProgress;
                break;
            case ProgressContextState.Complete:
                this.cleanclose = true;
                this.Close();
                break;
            case ProgressContextState.Cancelled:
                this.Close();
                break;
            default:
                break;
        }
    }

    private void b_cancel_Click(object sender, RoutedEventArgs e)
    {
        _context.State = ProgressContextState.Cancelled;
        this.Close();
    }
}
}
