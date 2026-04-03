# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概况

Unity 6 俯视角 3D 动作 Roguelike Demo (Dungeon Slash)，用于学习 3D 游戏开发基础并准备面试。

## 教学规则

**这是一个学习项目。** 用户的核心目标是掌握 3D 游戏开发知识，不只是完成 demo。因此：

- 每一个操作步骤都必须解释 **含义、作用和原理**
- 解释要达到"面试能说清楚"的程度
- 不要只说"把 X 设为 Y"，要说"把 X 设为 Y，因为..."
- 用 🎓 标记关键知识点
- 适时对比 2D 开发经验（用户有 Cocos 和 Godot 2D 背景）

## 用户背景

- 有 2D 游戏开发经验（Cocos、Godot）
- C# 有一些基础
- 正在面试游戏开发岗位，要求 3D 开发能力
- 使用 macOS

## 技术栈

- Unity 6 (Universal 3D / URP 模板)
- C#
- Cinemachine v4 (Unity 6 新版，API 与旧版不同)
- NavMesh, Animator, Particle System

## 素材

- Little Heroes Mega Pack v1.8.1（像素风低模角色包，自带丰富动画）
- 动画已确认齐全：Idle, Run, Dash, Melee Attack x3, Take Damage, Die, Crossbow Shoot 等

## Unity 6 注意事项

- Cinemachine 是 v4 版本，菜单在 GameObject → Cinemachine → Targeted Cameras → Follow Camera
- 没有 "Virtual Camera"，改为 "Cinemachine Camera"
- Follow Offset 替代了旧版的 Camera Distance
- 材质转换：Edit → Rendering → Materials → Convert Built-in Materials to URP（需先选中材质）
- URP Shader 属性名用 `_BaseColor` 而非 Built-in 的 `_Color`

## 文档位置

- 设计文档：`docs/superpowers/specs/2026-04-01-dungeon-slash-design.md`
- 实施计划：`docs/superpowers/plans/2026-04-01-dungeon-slash.md`
- 渲染优化笔记：`docs/rendering-optimization-notes.md`
