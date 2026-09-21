PREFIX   ?= /usr
DESTDIR  ?=
PKGNAME   = subsretimer
LIBDIR    = $(DESTDIR)$(PREFIX)/lib/$(PKGNAME)
BINDIR    = $(DESTDIR)$(PREFIX)/bin
APPDIR    = $(DESTDIR)$(PREFIX)/share/applications
LICDIR    = $(DESTDIR)$(PREFIX)/share/licenses/$(PKGNAME)
ICONDIR   = $(DESTDIR)$(PREFIX)/share/icons/hicolor
ICONSIZES = 16 32 48
PROJ      = SubsRetimer/SubsRetimer.csproj
PUBLISH   = SubsRetimer/bin/Release/net10.0/publish
TESTPROJ  = SubsRetimer.Tests/SubsRetimer.Tests.csproj
UITESTPROJ = SubsRetimer.UiTests/SubsRetimer.UiTests.csproj

.PHONY: build test test-ui install uninstall clean

build:
	dotnet publish $(PROJ) -c Release --no-self-contained

test:
	dotnet test $(TESTPROJ) -c Release

# Needs GTK 4 and a display: on a headless Linux box this wraps it in Xvfb.
test-ui:
	GSK_RENDERER=cairo xvfb-run -a dotnet test $(UITESTPROJ) -c Release

# The icon is installed under the name the desktop file and the window ask for
# ("subsretimer"); updating the icon cache is left to the packager.
install: build
	install -dm755 "$(LIBDIR)"
	cp -r $(PUBLISH)/* "$(LIBDIR)/"
	install -Dm755 dist/subsretimer.sh "$(BINDIR)/subsretimer"
	install -Dm644 dist/subsretimer.desktop "$(APPDIR)/subsretimer.desktop"
	install -Dm644 LICENSE "$(LICDIR)/LICENSE"
	for s in $(ICONSIZES); do \
	  install -Dm644 "assets/subsretimer-$$s.png" "$(ICONDIR)/$${s}x$${s}/apps/subsretimer.png"; \
	done

uninstall:
	rm -rf "$(LIBDIR)"
	rm -f  "$(BINDIR)/subsretimer"
	rm -f  "$(APPDIR)/subsretimer.desktop"
	rm -rf "$(LICDIR)"
	for s in $(ICONSIZES); do \
	  rm -f "$(ICONDIR)/$${s}x$${s}/apps/subsretimer.png"; \
	done

clean:
	dotnet clean $(PROJ) -c Release 2>/dev/null || true
	rm -rf SubsRetimer/bin SubsRetimer/obj SubsRetimer.Core/bin SubsRetimer.Core/obj
	rm -rf SubsRetimer.Tests/bin SubsRetimer.Tests/obj
	rm -rf SubsRetimer.Gtk/bin SubsRetimer.Gtk/obj
	rm -rf SubsRetimer.UiTests/bin SubsRetimer.UiTests/obj
