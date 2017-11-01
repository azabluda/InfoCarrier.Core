for /d /r . %%d in (artifacts,packages,bin,obj) do @if exist "%%d" rd /s /q "%%d"
