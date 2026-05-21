# Chemistry - Unity 化学编辑器

一个基于 Unity 开发的 3D 化学分子编辑器，支持原子与化学键的交互式创建、编辑和可视化。

## ✨ 功能特性

### 核心功能
- **15 种元素系统** - 支持氢、碳、氮、氧、硫、钠等多种化学元素
- **原子创建与删除** - 直观的原子操作界面
- **化学键创建** - 支持单键、双键、三键及虚线键
- **化学键旋转** - R 键启动旋转，Shift 键 15° 吸附，ESC 取消操作
- **撤销/重做** - Ctrl+Z / Ctrl+Y 完整的历史操作记录
- **网格吸附** - 精确的元素定位
- **存档读档** - 场景保存与加载功能

### 界面特性
- **中英双语支持** - UI 文本支持中英文切换
- **实时信息显示** - 选中元素时显示详细信息（键类型、旋转角度等）
- **Source Han Sans 字体** - 优质的中文显示效果

## 🎮 操作说明

| 操作 | 按键/说明 |
|------|-----------|
| 创建原子 | 点击元素按钮后在场景中放置 |
| 创建化学键 | 选中原子后拖拽到目标原子 |
| 旋转化学键 | 选中键后按 `R` 键启动旋转 |
| 精确旋转 | 旋转时按住 `Shift` 键启用 15° 吸附 |
| 取消操作 | `ESC` 键取消当前操作并取消选中 |
| 撤销 | `Ctrl + Z` |
| 重做 | `Ctrl + Y` |

## 🛠 技术栈

- **引擎**: Unity 2022+
- **语言**: C# (.NET 4.x+)
- **架构**: 命令模式（Command Pattern）实现撤销/重做系统
- **UI**: Unity UI (uGUI) + 自定义中文本地化

## 📦 安装运行

1. 克隆仓库到本地：
   ```bash
   git clone https://github.com/Tianmain/Chemistry.git
   ```

2. 使用 Unity Hub 打开项目（建议 Unity 2022.3 或更高版本）

3. 打开场景 `Assets/Scenes/AtomCreator.unity`

4. 点击 Play 运行

## 📁 项目结构

```
Assets/
├── Scripts/
│   ├── ChemistryEditor/     # 核心编辑器脚本
│   ├── History/             # 撤销重做系统
│   └── UI/                  # UI 管理脚本
├── Scenes/                  # Unity 场景文件
├── MaterialManager.asset    # 元素材质配置
└── SourceHanSansCN/         # 中文字体资源
```

## 📝 更新日志

- **2026-05** - 修复 MaterialManager 中 Sulfur/Sodium 材质 GUID 重复问题
- **2026-05** - 完善化学键旋转交互（R 键启动、ESC 取消、Shift 15° 吸附）
- **2026-05** - 添加 SaveSystemSetup Editor 工具
- **2026-04** - 初始版本发布

## 📄 许可证

本项目为个人学习研究项目，仅供学习交流使用。

## 🙋 联系方式

- GitHub: [@Tianmain](https://github.com/Tianmain)

---

⚗️ *用 Unity 让化学分子编辑更直观*
