打包脚本已放在项目根目录：

1. build_appx_package.ps1
作用：从 bin\x64\Debug\AppX 生成 TacticalRadarFinal.appx，并自动签名

2. install_appx_package.ps1
作用：导入测试证书并安装 TacticalRadarFinal.appx

使用顺序：

1. 先在 Visual Studio 里生成 Debug|x64，确保存在目录：
   bin\x64\Debug\AppX

2. 在 PowerShell 里执行：
   powershell -ExecutionPolicy Bypass -File .\build_appx_package.ps1

3. 安装：
   powershell -ExecutionPolicy Bypass -File .\install_appx_package.ps1

输出文件会生成在项目根目录：

- TacticalRadarFinal.appx
- TacticalRadarFinal_TestCert.pfx

测试证书密码：
123456
