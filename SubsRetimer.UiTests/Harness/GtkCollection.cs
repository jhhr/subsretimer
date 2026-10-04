//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace SubsRetimer.UiTests.Harness
{
  /// <summary>
  /// Every GTK test shares one <see cref="GtkFixture"/> (one main loop per
  /// process). Mark test classes with <c>[Collection(GtkCollection.Name)]</c>.
  /// </summary>
  [CollectionDefinition(Name)]
  public sealed class GtkCollection : ICollectionFixture<GtkFixture>
  {
    public const string Name = "Gtk";
  }
}
