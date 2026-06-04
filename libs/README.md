# Local Reference Assemblies

This folder is intentionally kept out of git except for this README.

The mod needs compile-time references from a local Tiny Combat Arena install and
from BepInEx. Do not commit or redistribute those DLLs here.

To populate this folder locally:

```powershell
.\scripts\CopyReferenceAssemblies.ps1 -GamePath "C:\Program Files (x86)\Steam\steamapps\common\Tiny Combat Arena"
```

If your BepInEx install is not inside the game folder, pass `-BepInExPath` too.
