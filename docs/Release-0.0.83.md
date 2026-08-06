# NovaOryn 0.0.83

## Updater carried-forward deletion correction

The updater now accepts a tracked path as a valid carried-forward deletion when the path is absent from the working tree and absent from the selected target source manifest. This avoids depending on the exact Git porcelain status column while retaining protection for files that still belong in the target release.
