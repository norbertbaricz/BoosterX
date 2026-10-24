# Windows implementation references

Primary references used for supported mechanisms and tradeoffs:

- [Windows uninstall registry](https://learn.microsoft.com/windows/win32/msi/uninstall-registry-key)
- [Windows Installer command-line and no-restart options](https://learn.microsoft.com/windows/win32/msi/standard-installer-command-line-options)
- [Package framework classification](https://learn.microsoft.com/uwp/api/windows.applicationmodel.package.isframework)
- [Edge startup boost policy](https://learn.microsoft.com/deployedge/microsoft-edge-policies/startupboostenabled)
- [Edge background mode policy](https://learn.microsoft.com/deployedge/microsoft-edge-browser-policies/backgroundmodeenabled)
- [Windows Storage Sense](https://support.microsoft.com/windows/experience/storage-filemanagement/manage-drive-space-with-storage-sense)
- [Windows drive optimization options](https://learn.microsoft.com/windows-server/administration/windows-commands/defrag)
- [Windows experience preferences and policy support](https://learn.microsoft.com/windows/client-management/mdm/policy-csp-experience)

Per-user Explorer/desktop preference support can vary by Windows build or policy;
stored-value verification is distinct from effective behavior and performance.
Use native Windows acceptance rather than treating a registry write as proof of
an improvement. No third-party cleaner/uninstaller or benchmark claim is implied.

Cleanup idle-check references (read-only service query; only STOPPED is idle):

- [QueryServiceStatus and required access](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-queryservicestatus)
- [SERVICE_STATUS values](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_status)
