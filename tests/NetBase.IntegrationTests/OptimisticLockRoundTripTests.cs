using Microsoft.Extensions.DependencyInjection;
using NetBase.Common.Exceptions;
using NetBase.Model.Dtos;
using NetBase.Service.Sys;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 乐观锁版本往返回归测试：每个接入乐观锁的模块连续编辑两次，
/// 第二次必须携带列表返回的最新版本成功——防止手工 DTO 映射漏 Version
/// 导致「首次编辑后永远冲突」（用户模块曾踩坑）。
/// </summary>
[Collection("Integration")]
public class OptimisticLockRoundTripTests
{
    private readonly IntegrationFixture _fixture;

    public OptimisticLockRoundTripTests(IntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task User_EditTwice_ShouldRoundTrip()
    {
        var userSvc = _fixture.GetService<ISysUserService>();
        var operatorName = "round-trip";

        var uid = await userSvc.CreateAsync(new NetBase.Model.Dtos.UserCreateDto
        {
            UserName = NetBase.IntegrationTests.IntegrationFixture.Uid("rtu"),
            Password = "Net123456",
            Status = 1
        }, operatorName);

        // 第一次编辑（库中 version=0）
        await userSvc.UpdateAsync(uid, new NetBase.Model.Dtos.UserUpdateDto
        {
            NickName = "第一次", Status = 1, Version = 0
        }, operatorName);

        // 列表/详情应返回 version=1（此前 UserDto 手工映射漏此字段导致永远为 0）
        var detail = await userSvc.GetDetailAsync(uid);
        Assert.NotNull(detail);
        Assert.Equal(1, detail!.Version);

        // 第二次编辑携带最新版本 → 成功
        await userSvc.UpdateAsync(uid, new NetBase.Model.Dtos.UserUpdateDto
        {
            NickName = "第二次", Status = 1, Version = 1
        }, operatorName);
        Assert.Equal(2, (await userSvc.GetDetailAsync(uid))!.Version);
    }

    [Fact]
    public async Task Role_EditTwice_ShouldRoundTrip()
    {
        var roleSvc = _fixture.GetService<ISysRoleService>();
        var operatorName = "round-trip";
        var code = NetBase.IntegrationTests.IntegrationFixture.Uid("rtr");

        var rid = await roleSvc.CreateAsync(new RoleSaveDto
        {
            RoleName = "往返角色", RoleCode = code, Status = 1
        }, operatorName);

        // 版本0 → 编辑 → 详情版本应为 1（Adapt 自动映射）
        var d1 = await roleSvc.GetDetailAsync(rid);
        Assert.Equal(0, d1!.Version);
        await roleSvc.UpdateAsync(rid, new RoleSaveDto
        {
            RoleName = "往返角色-改", RoleCode = code, Status = 1, Version = d1.Version
        }, operatorName);

        var d2 = await roleSvc.GetDetailAsync(rid);
        Assert.Equal(1, d2!.Version);
        await roleSvc.UpdateAsync(rid, new RoleSaveDto
        {
            RoleName = "往返角色-再改", RoleCode = code, Status = 1, Version = d2.Version
        }, operatorName);

        Assert.Equal(2, (await roleSvc.GetDetailAsync(rid))!.Version);
    }

    [Fact]
    public async Task Menu_EditTwice_ShouldRoundTrip()
    {
        var menuSvc = _fixture.GetService<ISysMenuService>();
        var operatorName = "round-trip";

        var mid = await menuSvc.CreateAsync(new MenuSaveDto
        {
            MenuName = NetBase.IntegrationTests.IntegrationFixture.Uid("往返菜单"),
            MenuType = 2, Path = "/rt", Component = "", Permission = ""
        }, operatorName);

        var d1 = await menuSvc.GetDetailAsync(mid);
        Assert.Equal(0, d1!.Version);
        await menuSvc.UpdateAsync(mid, new MenuSaveDto
        {
            MenuName = "往返菜单-改", MenuType = 2, Path = "/rt", Component = "", Permission = "", Version = d1.Version
        }, operatorName);

        var d2 = await menuSvc.GetDetailAsync(mid);
        Assert.Equal(1, d2!.Version);
        await menuSvc.UpdateAsync(mid, new MenuSaveDto
        {
            MenuName = "往返菜单-再改", MenuType = 2, Path = "/rt", Component = "", Permission = "", Version = d2.Version
        }, operatorName);

        Assert.Equal(2, (await menuSvc.GetDetailAsync(mid))!.Version);
    }
}
