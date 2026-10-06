using System;
using System.Collections.Generic;
using ContactTogetherApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ContactTogetherApi.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<TblAccount> TblAccounts { get; set; }

    public virtual DbSet<TblAccountDetail> TblAccountDetails { get; set; }

    public virtual DbSet<TblAccountType> TblAccountTypes { get; set; }

    public virtual DbSet<TblActivity> TblActivities { get; set; }

    public virtual DbSet<TblArea> TblAreas { get; set; }

    public virtual DbSet<TblAttachment> TblAttachments { get; set; }

    public virtual DbSet<TblBroadcast> TblBroadcasts { get; set; }

    public virtual DbSet<TblBroadcastGroup> TblBroadcastGroups { get; set; }

    public virtual DbSet<TblCategory> TblCategories { get; set; }

    public virtual DbSet<TblChannel> TblChannels { get; set; }

    public virtual DbSet<TblContact> TblContacts { get; set; }

    public virtual DbSet<TblEmployee> TblEmployees { get; set; }

    public virtual DbSet<TblEmployeeGroup> TblEmployeeGroups { get; set; }

    public virtual DbSet<TblErrorLog> TblErrorLogs { get; set; }

    public virtual DbSet<TblGender> TblGenders { get; set; }

    public virtual DbSet<TblLevel> TblLevels { get; set; }

    public virtual DbSet<TblLoginHistory> TblLoginHistories { get; set; }

    public virtual DbSet<TblOrganization> TblOrganizations { get; set; }

    public virtual DbSet<TblOrganizationGroup> TblOrganizationGroups { get; set; }

    public virtual DbSet<TblPage> TblPages { get; set; }

    public virtual DbSet<TblReference> TblReferences { get; set; }

    public virtual DbSet<TblRemark> TblRemarks { get; set; }

    public virtual DbSet<TblReopenedLog> TblReopenedLogs { get; set; }

    public virtual DbSet<TblReport> TblReports { get; set; }

    public virtual DbSet<TblReportParameter> TblReportParameters { get; set; }

    public virtual DbSet<TblRole> TblRoles { get; set; }

    public virtual DbSet<TblRolePage> TblRolePages { get; set; }

    public virtual DbSet<TblRunning> TblRunnings { get; set; }

    public virtual DbSet<TblService> TblServices { get; set; }

    public virtual DbSet<TblSession> TblSessions { get; set; }

    public virtual DbSet<TblStatus> TblStatuses { get; set; }

    public virtual DbSet<TblTmpSr> TblTmpSrs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Thai_100_CS_AI");

        modelBuilder.Entity<TblAccount>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("TblAccount");

            entity.Property(e => e.AreaId).HasMaxLength(50);
            entity.Property(e => e.Birthdate).HasMaxLength(50);
            entity.Property(e => e.Created).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.FirstnameEn).HasMaxLength(200);
            entity.Property(e => e.FirstnameTh).HasMaxLength(200);
            entity.Property(e => e.GenderId).HasMaxLength(50);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.IsEnable).HasMaxLength(50);
            entity.Property(e => e.IsScret).HasMaxLength(10);
            entity.Property(e => e.LastnameEn).HasMaxLength(200);
            entity.Property(e => e.LastnameTh).HasMaxLength(200);
            entity.Property(e => e.SalutationEn).HasMaxLength(100);
            entity.Property(e => e.SalutationTh).HasMaxLength(100);
            entity.Property(e => e.Updated).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.Property(e => e.Zipcode).HasMaxLength(100);
        });

        modelBuilder.Entity<TblAccountDetail>(entity =>
        {
            entity.ToTable("TblAccountDetail");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.AccountId).HasMaxLength(50);
            entity.Property(e => e.Created).HasMaxLength(20);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.DetailType).HasMaxLength(50);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.Updated).HasMaxLength(20);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblAccountType>(entity =>
        {
            entity.ToTable("TblAccountType");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Created).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.Updated).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblActivity>(entity =>
        {
            entity.HasKey(e => new { e.ServiceId, e.Line });

            entity.ToTable("TblActivity");

            entity.Property(e => e.ServiceId).HasMaxLength(50);
            entity.Property(e => e.CategoryId).HasMaxLength(50);
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.ContactId).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.GroupId).HasMaxLength(50);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.IsReceived).HasMaxLength(10);
            entity.Property(e => e.OwnerId).HasMaxLength(50);
            entity.Property(e => e.StatusId).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);

            entity.HasOne(d => d.Category).WithMany(p => p.TblActivities)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TblArea>(entity =>
        {
            entity.ToTable("TblArea");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.AreaType).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.RefId).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.Property(e => e.Zipcode).HasColumnType("decimal(18, 0)");

            entity.HasOne(d => d.Ref).WithMany(p => p.InverseRef)
                .HasForeignKey(d => d.RefId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TblAttachment>(entity =>
        {
            entity.ToTable("TblAttachment");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.FileType).HasMaxLength(100);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.ServiceId).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblBroadcast>(entity =>
        {
            entity.ToTable("TblBroadcast");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.BroadcastType).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.HaveGroup).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.TitleEn).HasMaxLength(250);
            entity.Property(e => e.TitleTh).HasMaxLength(250);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblBroadcastGroup>(entity =>
        {
            entity.ToTable("TblBroadcastGroup");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.BroadcastId).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);

            entity.HasOne(d => d.Broadcast).WithMany(p => p.TblBroadcastGroups).HasForeignKey(d => d.BroadcastId);
        });

        modelBuilder.Entity<TblCategory>(entity =>
        {
            entity.ToTable("TblCategory");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CategoryType).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.RefId).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);

            entity.HasOne(d => d.Ref).WithMany(p => p.InverseRef)
                .HasForeignKey(d => d.RefId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TblChannel>(entity =>
        {
            entity.ToTable("TblChannel");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Created).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.Updated).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(150);
        });

        modelBuilder.Entity<TblContact>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("TblContact");

            entity.Property(e => e.CategoryId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ChannelId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ContactDetail)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ContactEnd)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ContactStart)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ID");
            entity.Property(e => e.MenuIvr)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("MenuIVR");
        });

        modelBuilder.Entity<TblEmployee>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("TblEmployee");

            entity.Property(e => e.Birthdate)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ContactDetail)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Created)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DateExpire)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DateHire)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DefaultLanguage)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DefaultRowPerPage)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FirstnameEn)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FirstnameTh)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GenderId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsEnable)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.LastnameEn)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.LastnameTh)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OrganizationId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PictureProfile)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Position)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.RoleId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SalutationEn)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SalutationTh)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Updated)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserPassword)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblEmployeeGroup>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("TblEmployeeGroup");

            entity.Property(e => e.EmployeeId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GroupId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblErrorLog>(entity =>
        {
            entity.ToTable("TblErrorLog");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.ErrorCode).HasMaxLength(50);
            entity.Property(e => e.ErrorOnFunction).HasMaxLength(255);
            entity.Property(e => e.ErrorOnPage).HasMaxLength(255);
            entity.Property(e => e.UserId).HasMaxLength(50);
        });

        modelBuilder.Entity<TblGender>(entity =>
        {
            entity.ToTable("TblGender");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Created).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.Updated).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblLevel>(entity =>
        {
            entity.ToTable("TblLevel");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.LevelType).HasMaxLength(50);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblLoginHistory>(entity =>
        {
            entity.ToTable("TblLoginHistory");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.EmployeeId).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserName).HasMaxLength(100);
        });

        modelBuilder.Entity<TblOrganization>(entity =>
        {
            entity.ToTable("TblOrganization");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Created).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.RefId).HasMaxLength(50);
            entity.Property(e => e.Updated).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);

            entity.HasOne(d => d.Ref).WithMany(p => p.InverseRef)
                .HasForeignKey(d => d.RefId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TblOrganizationGroup>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("TblOrganizationGroup");

            entity.Property(e => e.Created)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GroupId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GroupName)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IsDefault)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsEnable)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RefGroupId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Updated)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblPage>(entity =>
        {
            entity.ToTable("TblPage");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.ControlId).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.PageAdmin).HasMaxLength(10);
            entity.Property(e => e.PageFileName).HasMaxLength(255);
            entity.Property(e => e.PageNameEn).HasMaxLength(200);
            entity.Property(e => e.PageNameTh).HasMaxLength(200);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblReference>(entity =>
        {
            entity.ToTable("TblReference");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblRemark>(entity =>
        {
            entity.ToTable("TblRemark");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Abbreviation).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblReopenedLog>(entity =>
        {
            entity.HasKey(e => new { e.SrId, e.Line });

            entity.ToTable("TblReopenedLog");

            entity.Property(e => e.SrId).HasMaxLength(50);
            entity.Property(e => e.ClosedBy).HasMaxLength(100);
            entity.Property(e => e.ReopenedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblReport>(entity =>
        {
            entity.HasKey(e => e.ReportId);

            entity.ToTable("TblReport");

            entity.Property(e => e.ReportId).HasMaxLength(50);
            entity.Property(e => e.Displaygrouptree).HasMaxLength(10);
            entity.Property(e => e.Enable).HasMaxLength(10);
            entity.Property(e => e.Recuser).HasMaxLength(100);
            entity.Property(e => e.ReportDatabasename).HasMaxLength(100);
            entity.Property(e => e.ReportFileName).HasMaxLength(255);
            entity.Property(e => e.ReportIntegratedsecurity).HasMaxLength(50);
            entity.Property(e => e.ReportName).HasMaxLength(200);
            entity.Property(e => e.ReportPassword).HasMaxLength(255);
            entity.Property(e => e.ReportServername).HasMaxLength(100);
            entity.Property(e => e.ReportUsername).HasMaxLength(100);
            entity.Property(e => e.Target).HasMaxLength(100);
            entity.Property(e => e.Updateuser).HasMaxLength(100);
        });

        modelBuilder.Entity<TblReportParameter>(entity =>
        {
            entity.ToTable("TblReportParameter");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.DatabaseType).HasMaxLength(50);
            entity.Property(e => e.Enable).HasMaxLength(10);
            entity.Property(e => e.ObjectType).HasMaxLength(50);
            entity.Property(e => e.ParameterDefault).HasMaxLength(255);
            entity.Property(e => e.ParameterName).HasMaxLength(100);
            entity.Property(e => e.ParameterType).HasMaxLength(50);
            entity.Property(e => e.Postback).HasMaxLength(10);
            entity.Property(e => e.Recuser).HasMaxLength(100);
            entity.Property(e => e.ReportId).HasMaxLength(50);
            entity.Property(e => e.Updateuser).HasMaxLength(100);

            entity.HasOne(d => d.Report).WithMany(p => p.TblReportParameters).HasForeignKey(d => d.ReportId);
        });

        modelBuilder.Entity<TblRole>(entity =>
        {
            entity.ToTable("TblRole");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Created).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsAdmin).HasMaxLength(10);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.LevelSecretId).HasMaxLength(50);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.Updated).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblRolePage>(entity =>
        {
            entity.ToTable("TblRolePage");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsAdmin).HasMaxLength(10);
            entity.Property(e => e.IsDelete).HasMaxLength(10);
            entity.Property(e => e.IsDownload).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.IsInsert).HasMaxLength(10);
            entity.Property(e => e.IsOpen).HasMaxLength(10);
            entity.Property(e => e.IsPrint).HasMaxLength(10);
            entity.Property(e => e.IsSearch).HasMaxLength(10);
            entity.Property(e => e.IsUpdate).HasMaxLength(10);
            entity.Property(e => e.PageId).HasMaxLength(50);
            entity.Property(e => e.RoleId).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);

            entity.HasOne(d => d.Role).WithMany(p => p.TblRolePages).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<TblRunning>(entity =>
        {
            entity.ToTable("TblRunning");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.RunningCode).HasMaxLength(50);
            entity.Property(e => e.RunningFormat).HasMaxLength(100);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<TblService>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("TblService");

            entity.Property(e => e.AccountId)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Account_ID");
            entity.Property(e => e.AreaId)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Area_ID");
            entity.Property(e => e.CallBack)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Call_Back");
            entity.Property(e => e.CategoryId)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Category_ID");
            entity.Property(e => e.ChannelIncomingId)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Channel_Incoming_ID");
            entity.Property(e => e.ChannelOutgoingId)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Channel_Outgoing_ID");
            entity.Property(e => e.Code)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Created)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Created_By");
            entity.Property(e => e.DateClosed)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("Date_Closed");
            entity.Property(e => e.DateOpened)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("Date_Opened");
            entity.Property(e => e.Detail).HasMaxLength(1000);
            entity.Property(e => e.Id)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.IsEnable)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("Is_Enable");
            entity.Property(e => e.LevelPriorityId)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("Level_Priority_ID");
            entity.Property(e => e.LevelSecretId)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("Level_Secret_ID");
            entity.Property(e => e.LevelSeverityId)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("Level_Severity_ID");
            entity.Property(e => e.OnScene)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("On_Scene");
            entity.Property(e => e.OrganizationId)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Organization_ID");
            entity.Property(e => e.OwnerId)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Owner_ID");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.ServiceArea)
                .HasMaxLength(2000)
                .IsUnicode(false)
                .HasColumnName("Service_Area");
            entity.Property(e => e.ServiceReference)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("Service_Reference");
            entity.Property(e => e.ServiceReferenceLink)
                .HasMaxLength(500)
                .HasColumnName("Service_Reference_Link");
            entity.Property(e => e.StatusId)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Status_ID");
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.Updated)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("Updated_By");
        });

        modelBuilder.Entity<TblSession>(entity =>
        {
            entity.ToTable("TblSession");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.EmployeeId).HasMaxLength(50);
            entity.Property(e => e.ExpiresAt).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.LastActiveAt).HasMaxLength(50);
            entity.Property(e => e.ServerName).HasMaxLength(100);
            entity.Property(e => e.SessionTime).HasMaxLength(50);
            entity.Property(e => e.TokenHash).HasMaxLength(64);
        });

        modelBuilder.Entity<TblStatus>(entity =>
        {
            entity.ToTable("TblStatus");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Created).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasMaxLength(10);
            entity.Property(e => e.IsEnable).HasMaxLength(10);
            entity.Property(e => e.IsType).HasMaxLength(10);
            entity.Property(e => e.NameEn).HasMaxLength(200);
            entity.Property(e => e.NameTh).HasMaxLength(200);
            entity.Property(e => e.RefId).HasMaxLength(50);
            entity.Property(e => e.StatusType).HasMaxLength(50);
            entity.Property(e => e.Updated).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);

            entity.HasOne(d => d.Ref).WithMany(p => p.InverseRef)
                .HasForeignKey(d => d.RefId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TblTmpSr>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("TblTmpSr");

            entity.Property(e => e.SrId).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
