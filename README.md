[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder) [![GitHub Repo stars](https://img.shields.io/github/stars/deanthecoder/Wolfenshine?style=social&label=Star)](https://github.com/deanthecoder/Wolfenshine/stargazers)

# Wolfenshine

A modern C# reimplementation of Wolfenstein 3D, beginning with a faithful 320×200 software renderer and leaving room for enhanced GPU rendering later.

## Status

Wolfenshine is in its earliest development stage. The repository currently contains a runnable Avalonia desktop shell, an MVVM foundation, and an NUnit test project.

## Building

Clone the repository and its submodules, then build the solution:

```shell
git clone --recurse-submodules https://github.com/deanthecoder/Wolfenshine.git
cd Wolfenshine
dotnet build Wolfenshine.slnx
```

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
