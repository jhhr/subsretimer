//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

// One GTK main loop per process, and the windows share the display: tests must
// never run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
