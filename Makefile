PREFIX   ?= /usr
DESTDIR  ?=
PKGNAME   = subsretimer
LIBDIR    = $(DESTDIR)$(PREFIX)/lib/$(PKGNAME)
BINDIR    = $(DESTDIR)$(PREFIX)/bin
APPDIR    = $(DESTDIR)$(PREFIX)/share/applications
LICDIR    = $(DESTDIR)$(PREFIX)/share/licenses/$(PKGNAME)
PROJ      = SubsRetimer/SubsRetimer.csproj
PUBLISH   = SubsRetimer/bin/Release/net10.0/publish
TESTPROJ  = SubsRetimer.Tests/SubsRetimer.Tests.csproj

.PHONY: build test install uninstall clean

build:
	dotnet publish $(PROJ) -c Release --no-self-contained

test:
	dotnet test $(TESTPROJ) -c Release

install: build
	install -dm755 "$(LIBDIR)"
	cp -r $(PUBLISH)/* "$(LIBDIR)/"
	install -Dm755 dist/subsretimer.sh "$(BINDIR)/subsretimer"
	install -Dm644 dist/subsretimer.desktop "$(APPDIR)/subsretimer.desktop"
	install -Dm644 LICENSE "$(LICDIR)/LICENSE"

uninstall:
	rm -rf "$(LIBDIR)"
	rm -f  "$(BINDIR)/subsretimer"
	rm -f  "$(APPDIR)/subsretimer.desktop"
	rm -rf "$(LICDIR)"

clean:
	dotnet clean $(PROJ) -c Release 2>/dev/null || true
	rm -rf SubsRetimer/bin SubsRetimer/obj SubsRetimer.Core/bin SubsRetimer.Core/obj
	rm -rf SubsRetimer.Tests/bin SubsRetimer.Tests/obj
