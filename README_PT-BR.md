# PS3Recomp — projeto Windows / Visual Studio 2022

Este pacote combina o código original do `ps3recomp` com uma solução Visual Studio e um script que automatiza a análise, o lifting PPU e a compilação CMake para Windows x64.

## Arquivos principais

- `PS3RecompWindows.sln` — solução para abrir no Visual Studio 2022.
- `PS3RecompBuild.vcxproj` — projeto de build que chama o pipeline PowerShell.
- `CMakePresets.json` — presets para compilar o runtime com VS 2022 ou clang-cl + Ninja.
- `BUILD_PS3_GAME.ps1` — executa loader, lifter, geração de tabela HLE e build do jogo.
- `ps3recomp-master/` — código-fonte original completo.
- `input/EBOOT.ELF` — ELF fornecido para esta tentativa.
- `ports/EBOOT/analysis/` — análise inicial do ELF já feita.

## Pré-requisitos no Windows

1. Visual Studio 2022 com **Desenvolvimento para desktop com C++** e Windows SDK.
2. LLVM/Clang, incluindo `clang-cl`.
3. CMake 3.25+ (o projeto original requer 3.20+) e Ninja.
4. Python 3.10+.

Abra **Developer PowerShell for VS 2022** na pasta extraída do pacote e execute:

```powershell
python -m pip install -r .\ps3recomp-master\tools\requirements.txt
.\BUILD_PS3_GAME.ps1
```

O script usa `input\EBOOT.ELF` por padrão. Para outro ELF:

```powershell
.\BUILD_PS3_GAME.ps1 -EbootPath "D:\Jogos\MeuJogo\EBOOT.ELF"
```

Também pode abrir `PS3RecompWindows.sln` no Visual Studio, escolher `Release | x64` e usar **Build > Build Solution**. Esse projeto do Visual Studio orquestra o pipeline CMake/Ninja; não compila o código gerado como um projeto C++ convencional dentro do `.sln`.

## Saída esperada

A pasta de trabalho específica do título será `ports\EBOOT\`. O executável, se todas as etapas terminarem, ficará em `ports\EBOOT\build\` (normalmente `MyGameRecomp.exe`).

Para refazer do zero:

```powershell
.\BUILD_PS3_GAME.ps1 -Force
```

**Atenção:** a análise inicial deste ELF foi concluída e encontrou 18.798 descritores OPD, 29.917 funções candidatas após recuperação de limites e 262 imports de firmware em 19 bibliotecas. O lifting é a etapa pesada; ele pode demorar bastante e gerar centenas de megabytes de C/C++. A compilação também pode revelar imports, funções HLE, imagens SPU ou recursos gráficos que exigem trabalho específico do jogo. Gerar um EXE não garante que o jogo inicie ou seja jogável.

Este ambiente conseguiu analisar o ELF, mas não concluiu o lifting dentro do limite de execução disponível e não gerou `jogo.exe`. O pacote prepara a etapa de compilação para ser executada no Windows com as ferramentas instaladas.

## Execução por arquivo BAT

Dê duplo clique em `BUILD_PS3_GAME.bat` para abrir o menu. A opção 1 inicia o script PowerShell de análise, lifting e compilação usando `input\EBOOT.ELF`; a opção 2 abre a solução do Visual Studio; a opção 3 verifica se Python, CMake, Ninja e clang-cl estão no PATH. Para evitar problemas com ferramentas do compilador, prefira abrir o Developer Command Prompt/PowerShell do Visual Studio 2022 e executar o BAT a partir dali.

O BAT automatiza a execução, mas não instala dependências nem garante um EXE funcional. O pipeline pode parar se houver dependências ausentes ou funções do jogo ainda não suportadas.


## Compilacao sem PowerShell

Use `BUILD_PS3_GAME.bat`. O arquivo executa diretamente Python, CMake, Ninja e clang-cl; nao chama PowerShell nem depende de arquivos `.ps1`. Coloque o ELF descriptografado em `input\EBOOT.ELF` e execute o BAT pelo Developer Command Prompt do Visual Studio 2022, com as ferramentas instaladas. A geracao de um executavel nao garante compatibilidade completa do jogo.
