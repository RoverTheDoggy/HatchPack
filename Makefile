CONFIGURATION = Release

all: build

clean:
	@rm -rf bin/
	@rm -rf obj/

rebuild: clean all

build:
	msbuild /nologo /verbosity:minimal -p:Configuration=$(CONFIGURATION) HatchPack.csproj
