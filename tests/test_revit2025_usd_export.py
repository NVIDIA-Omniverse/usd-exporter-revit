# SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0


from test_revit_usd_export import TestRevitUsdExport


class TestRevit2025UsdExport(TestRevitUsdExport):
    """Test Revit USD Export Plugin with Revit 2025."""

    VER = "2025"

    def setUp(self):
        """Prepare the test fixture"""
        self._ver = TestRevit2025UsdExport.VER
        super().setUp()

