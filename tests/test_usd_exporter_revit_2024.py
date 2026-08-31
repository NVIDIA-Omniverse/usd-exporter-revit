# SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0


from test_usd_exporter_revit import TestUsdExporterRevit


class TestUsdExporterRevit2024(TestUsdExporterRevit):
    """Test usd-exporter-revit with Revit 2024."""

    VER = "2024"

    def setUp(self):
        """Prepare the test fixture"""
        self._ver = TestUsdExporterRevit2024.VER
        super().setUp()
