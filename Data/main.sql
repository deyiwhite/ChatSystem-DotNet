/*
 Navicat Premium Dump SQL

 Source Server         : chatsystem
 Source Server Type    : SQLite
 Source Server Version : 3045000 (3.45.0)
 Source Schema         : main

 Target Server Type    : SQLite
 Target Server Version : 3045000 (3.45.0)
 File Encoding         : 65001

 Date: 22/06/2026 14:01:00
*/

PRAGMA foreign_keys = false;

-- ----------------------------
-- Table structure for ChatGroups
-- ----------------------------
DROP TABLE IF EXISTS "ChatGroups";
CREATE TABLE "ChatGroups" (
  "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "Name" TEXT NOT NULL,
  "OwnerId" INTEGER NOT NULL,
  "CreatedAt" TEXT NOT NULL,
  CONSTRAINT "FK_ChatGroups_Users_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE RESTRICT ON UPDATE NO ACTION
);

-- ----------------------------
-- Records of ChatGroups
-- ----------------------------
INSERT INTO "ChatGroups" VALUES (2, 'palette', 2, '2026-06-21 22:41:11.9470339');
INSERT INTO "ChatGroups" VALUES (11, '我与panpan', 5, '2026-06-22 10:56:33.3802999');

-- ----------------------------
-- Table structure for FriendRequests
-- ----------------------------
DROP TABLE IF EXISTS "FriendRequests";
CREATE TABLE "FriendRequests" (
  "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "FromUserId" INTEGER NOT NULL,
  "ToUserId" INTEGER NOT NULL,
  "Status" INTEGER NOT NULL,
  "CreatedAt" TEXT NOT NULL,
  "HandledAt" TEXT,
  CONSTRAINT "FK_FriendRequests_Users_FromUserId" FOREIGN KEY ("FromUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT ON UPDATE NO ACTION,
  CONSTRAINT "FK_FriendRequests_Users_ToUserId" FOREIGN KEY ("ToUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT ON UPDATE NO ACTION
);

-- ----------------------------
-- Records of FriendRequests
-- ----------------------------
INSERT INTO "FriendRequests" VALUES (1, 2, 4, 2, '2026-06-21 14:35:45.3443384', '2026-06-21 22:41:50.0546225');
INSERT INTO "FriendRequests" VALUES (2, 5, 2, 2, '2026-06-21 22:55:53.7286388', '2026-06-21 22:56:16.9286838');
INSERT INTO "FriendRequests" VALUES (3, 5, 3, 2, '2026-06-21 22:55:57.9041315', '2026-06-21 22:57:13.3855705');
INSERT INTO "FriendRequests" VALUES (4, 4, 5, 2, '2026-06-21 22:57:38.7998419', '2026-06-21 22:58:14.4734425');
INSERT INTO "FriendRequests" VALUES (5, 5, 7, 3, '2026-06-22 10:54:16.3776632', '2026-06-22 11:04:59.6424611');
INSERT INTO "FriendRequests" VALUES (6, 5, 7, 1, '2026-06-22 11:19:08.2661214', NULL);
INSERT INTO "FriendRequests" VALUES (7, 5, 3, 1, '2026-06-22 11:42:00.3587733', NULL);

-- ----------------------------
-- Table structure for Friends
-- ----------------------------
DROP TABLE IF EXISTS "Friends";
CREATE TABLE "Friends" (
  "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "UserId" INTEGER NOT NULL,
  "FriendId" INTEGER NOT NULL,
  "CreatedAt" TEXT NOT NULL,
  CONSTRAINT "FK_Friends_Users_FriendId" FOREIGN KEY ("FriendId") REFERENCES "Users" ("Id") ON DELETE RESTRICT ON UPDATE NO ACTION,
  CONSTRAINT "FK_Friends_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT ON UPDATE NO ACTION
);

-- ----------------------------
-- Records of Friends
-- ----------------------------
INSERT INTO "Friends" VALUES (1, 2, 3, '2026-06-21 00:56:41.1412863');
INSERT INTO "Friends" VALUES (2, 3, 2, '2026-06-21 00:56:41.1472409');
INSERT INTO "Friends" VALUES (5, 3, 4, '2026-06-21 00:56:41.1488243');
INSERT INTO "Friends" VALUES (6, 4, 3, '2026-06-21 00:56:41.1492608');
INSERT INTO "Friends" VALUES (9, 2, 4, '2026-06-21 22:41:50.0547038');
INSERT INTO "Friends" VALUES (10, 4, 2, '2026-06-21 22:41:50.0547038');
INSERT INTO "Friends" VALUES (11, 5, 2, '2026-06-21 22:56:16.9288195');
INSERT INTO "Friends" VALUES (12, 2, 5, '2026-06-21 22:56:16.9288195');

-- ----------------------------
-- Table structure for GroupMembers
-- ----------------------------
DROP TABLE IF EXISTS "GroupMembers";
CREATE TABLE "GroupMembers" (
  "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "GroupId" INTEGER NOT NULL,
  "UserId" INTEGER NOT NULL,
  "JoinedAt" TEXT NOT NULL,
  CONSTRAINT "FK_GroupMembers_ChatGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ChatGroups" ("Id") ON DELETE CASCADE ON UPDATE NO ACTION,
  CONSTRAINT "FK_GroupMembers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT ON UPDATE NO ACTION
);

-- ----------------------------
-- Records of GroupMembers
-- ----------------------------
INSERT INTO "GroupMembers" VALUES (4, 2, 2, '2026-06-21 22:41:11.9470339');
INSERT INTO "GroupMembers" VALUES (5, 2, 3, '2026-06-21 22:41:11.9470339');
INSERT INTO "GroupMembers" VALUES (6, 2, 4, '2026-06-21 22:42:20.7176296');
INSERT INTO "GroupMembers" VALUES (14, 2, 5, '2026-06-21 22:58:37.288758');
INSERT INTO "GroupMembers" VALUES (28, 11, 5, '2026-06-22 10:56:33.3802999');
INSERT INTO "GroupMembers" VALUES (30, 11, 2, '2026-06-22 11:02:52.9073752');

-- ----------------------------
-- Table structure for GroupMessages
-- ----------------------------
DROP TABLE IF EXISTS "GroupMessages";
CREATE TABLE "GroupMessages" (
  "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "GroupId" INTEGER NOT NULL,
  "FromUserId" INTEGER NOT NULL,
  "Content" TEXT NOT NULL,
  "Type" INTEGER NOT NULL,
  "AttachmentFileName" TEXT,
  "AttachmentStoredName" TEXT,
  "AttachmentSize" INTEGER,
  "SentAt" TEXT NOT NULL,
  "IsDeletedByAdmin" INTEGER NOT NULL,
  CONSTRAINT "FK_GroupMessages_ChatGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ChatGroups" ("Id") ON DELETE CASCADE ON UPDATE NO ACTION,
  CONSTRAINT "FK_GroupMessages_Users_FromUserId" FOREIGN KEY ("FromUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT ON UPDATE NO ACTION
);

-- ----------------------------
-- Records of GroupMessages
-- ----------------------------
INSERT INTO "GroupMessages" VALUES (3, 2, 2, '大家好', 0, NULL, NULL, NULL, '2026-06-21 22:41:16.3733797', 0);
INSERT INTO "GroupMessages" VALUES (5, 2, 3, '你好', 0, NULL, NULL, NULL, '2026-06-21 22:57:19.9480564', 0);
INSERT INTO "GroupMessages" VALUES (8, 2, 5, '大家好，我是panpan', 0, NULL, NULL, NULL, '2026-06-22 10:35:58.0046417', 0);
INSERT INTO "GroupMessages" VALUES (9, 11, 5, '大家好，我是panpan', 0, NULL, NULL, NULL, '2026-06-22 11:16:04.7254961', 0);
INSERT INTO "GroupMessages" VALUES (10, 11, 5, '怎么样呢', 0, NULL, NULL, NULL, '2026-06-22 11:17:47.9414013', 0);
INSERT INTO "GroupMessages" VALUES (11, 11, 5, '大家自我介绍一下把', 0, NULL, NULL, NULL, '2026-06-22 11:17:59.9707683', 0);

-- ----------------------------
-- Table structure for Messages
-- ----------------------------
DROP TABLE IF EXISTS "Messages";
CREATE TABLE "Messages" (
  "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "FromUserId" INTEGER NOT NULL,
  "ToUserId" INTEGER NOT NULL,
  "Content" TEXT NOT NULL,
  "SentAt" TEXT NOT NULL,
  "IsDeletedBySender" INTEGER NOT NULL,
  "IsDeletedByReceiver" INTEGER NOT NULL,
  "IsDeletedByAdmin" INTEGER NOT NULL,
  "AttachmentFileName" TEXT,
  "AttachmentSize" INTEGER,
  "AttachmentStoredName" TEXT,
  "Type" INTEGER NOT NULL DEFAULT 0,
  CONSTRAINT "FK_Messages_Users_FromUserId" FOREIGN KEY ("FromUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT ON UPDATE NO ACTION,
  CONSTRAINT "FK_Messages_Users_ToUserId" FOREIGN KEY ("ToUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT ON UPDATE NO ACTION
);

-- ----------------------------
-- Records of Messages
-- ----------------------------
INSERT INTO "Messages" VALUES (1, 2, 3, '05-网络工程的实施-3~5.pdf', '2026-06-21 15:13:16.8588525', 0, 0, 0, '05-网络工程的实施-3~5.pdf', 3602992, 'a54423c0fa6d43f88d09117cb43241b3.pdf', 1);
INSERT INTO "Messages" VALUES (2, 2, 3, '你好，黑黑，我是白白', '2026-06-21 22:45:45.79703', 0, 0, 0, NULL, NULL, NULL, 0);
INSERT INTO "Messages" VALUES (3, 2, 4, '你好，小红，我是白白', '2026-06-21 22:45:53.4680063', 0, 0, 0, NULL, NULL, NULL, 0);
INSERT INTO "Messages" VALUES (4, 5, 2, '你好，我是panpan', '2026-06-21 22:58:19.4129156', 0, 0, 1, NULL, NULL, NULL, 0);
INSERT INTO "Messages" VALUES (5, 5, 3, '你好', '2026-06-21 23:45:37.6560108', 0, 0, 0, NULL, NULL, NULL, 0);
INSERT INTO "Messages" VALUES (6, 5, 4, '你好', '2026-06-21 23:45:41.3533767', 0, 0, 0, NULL, NULL, NULL, 0);
INSERT INTO "Messages" VALUES (7, 5, 2, '白白你好', '2026-06-22 10:54:44.4739865', 0, 0, 0, NULL, NULL, NULL, 0);

-- ----------------------------
-- Table structure for Users
-- ----------------------------
DROP TABLE IF EXISTS "Users";
CREATE TABLE "Users" (
  "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "Username" TEXT NOT NULL,
  "PasswordHash" TEXT NOT NULL,
  "DisplayName" TEXT NOT NULL,
  "Role" INTEGER NOT NULL,
  "Status" INTEGER NOT NULL,
  "CreatedAt" TEXT NOT NULL
);

-- ----------------------------
-- Records of Users
-- ----------------------------
INSERT INTO "Users" VALUES (1, 'admin', 'PBKDF2$100000$Q2hhdFN5c3RlbUFkbWluIQ==$hL6MKVryE7t/vL14Ch37RAA+Oo5gHZFtECAHhsk/H7Y=', '管理员', 1, 2, '2026-01-01 00:00:00');
INSERT INTO "Users" VALUES (2, 'user1', 'PBKDF2$100000$4ZgflU0ZGX+/7AWY0Rd20Q==$e3di309Iiw0pjghTUxnfjn4AAavDHNvK69rSt1XXw+o=', '白白', 2, 2, '2026-06-21 00:56:41.0322641');
INSERT INTO "Users" VALUES (3, 'user2', 'PBKDF2$100000$nAcmBRPnwNOmug7ZMk1Ybw==$PM+XZurnuF9kLDuiHuyPfaJ97fCRRFVA5PMDpIlQ+pw=', '黑黑', 2, 2, '2026-06-21 00:56:41.1031944');
INSERT INTO "Users" VALUES (4, 'user3', 'PBKDF2$100000$+/lFeW3l7IZh8tE0N6ORXQ==$SLQ1FODfdkokw1v2o9QxHx7JY8lFfVWzoiR7+BYlpVA=', '小红', 2, 2, '2026-06-21 00:56:41.1231087');
INSERT INTO "Users" VALUES (5, 'panpan', 'PBKDF2$100000$4lYF3c+okgxAOnKS4oiKKg==$vqWa/fLlqprma8Ngq5w+t0pJxMbveEZW/a1kUh7AKzk=', 'Vivian', 2, 2, '2026-06-21 22:55:24.5305286');
INSERT INTO "Users" VALUES (6, 'howareryu', 'PBKDF2$100000$hj4V7M0PrvUvSO/zIG5fUA==$7IRTeL3DHyMSHbpSILEtwrU9R8x+///tjgcdrIvqDM0=', 'RYUJIN', 2, 4, '2026-06-22 10:14:19.395408');
INSERT INTO "Users" VALUES (7, 'pankumo', 'PBKDF2$100000$jXcPz6XJ0XdnjE2taJ5v3Q==$qmVWfAKVQ0p5Z4nRbaxQ+eE5v5CKkmiug8B9WCYASJs=', '潘芸', 2, 2, '2026-06-22 10:33:10.1814079');

-- ----------------------------
-- Table structure for __EFMigrationsHistory
-- ----------------------------
DROP TABLE IF EXISTS "__EFMigrationsHistory";
CREATE TABLE "__EFMigrationsHistory" (
  "MigrationId" TEXT NOT NULL,
  "ProductVersion" TEXT NOT NULL,
  CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- ----------------------------
-- Records of __EFMigrationsHistory
-- ----------------------------
INSERT INTO "__EFMigrationsHistory" VALUES ('20260617025622_InitialCreate', '8.0.17');
INSERT INTO "__EFMigrationsHistory" VALUES ('20260620161136_AddGroupsAndFileMessages', '8.0.17');

-- ----------------------------
-- Table structure for sqlite_sequence
-- ----------------------------
DROP TABLE IF EXISTS "sqlite_sequence";
CREATE TABLE "sqlite_sequence" (
  "name",
  "seq"
);

-- ----------------------------
-- Records of sqlite_sequence
-- ----------------------------
INSERT INTO "sqlite_sequence" VALUES ('Users', 7);
INSERT INTO "sqlite_sequence" VALUES ('Friends', 16);
INSERT INTO "sqlite_sequence" VALUES ('ChatGroups', 12);
INSERT INTO "sqlite_sequence" VALUES ('GroupMembers', 33);
INSERT INTO "sqlite_sequence" VALUES ('GroupMessages', 11);
INSERT INTO "sqlite_sequence" VALUES ('FriendRequests', 7);
INSERT INTO "sqlite_sequence" VALUES ('Messages', 7);

-- ----------------------------
-- Auto increment value for ChatGroups
-- ----------------------------
UPDATE "sqlite_sequence" SET seq = 12 WHERE name = 'ChatGroups';

-- ----------------------------
-- Indexes structure for table ChatGroups
-- ----------------------------
CREATE INDEX "IX_ChatGroups_OwnerId"
ON "ChatGroups" (
  "OwnerId" ASC
);

-- ----------------------------
-- Auto increment value for FriendRequests
-- ----------------------------
UPDATE "sqlite_sequence" SET seq = 7 WHERE name = 'FriendRequests';

-- ----------------------------
-- Indexes structure for table FriendRequests
-- ----------------------------
CREATE INDEX "IX_FriendRequests_FromUserId_ToUserId_Status"
ON "FriendRequests" (
  "FromUserId" ASC,
  "ToUserId" ASC,
  "Status" ASC
);
CREATE INDEX "IX_FriendRequests_ToUserId"
ON "FriendRequests" (
  "ToUserId" ASC
);

-- ----------------------------
-- Auto increment value for Friends
-- ----------------------------
UPDATE "sqlite_sequence" SET seq = 16 WHERE name = 'Friends';

-- ----------------------------
-- Indexes structure for table Friends
-- ----------------------------
CREATE INDEX "IX_Friends_FriendId"
ON "Friends" (
  "FriendId" ASC
);
CREATE UNIQUE INDEX "IX_Friends_UserId_FriendId"
ON "Friends" (
  "UserId" ASC,
  "FriendId" ASC
);

-- ----------------------------
-- Auto increment value for GroupMembers
-- ----------------------------
UPDATE "sqlite_sequence" SET seq = 33 WHERE name = 'GroupMembers';

-- ----------------------------
-- Indexes structure for table GroupMembers
-- ----------------------------
CREATE UNIQUE INDEX "IX_GroupMembers_GroupId_UserId"
ON "GroupMembers" (
  "GroupId" ASC,
  "UserId" ASC
);
CREATE INDEX "IX_GroupMembers_UserId"
ON "GroupMembers" (
  "UserId" ASC
);

-- ----------------------------
-- Auto increment value for GroupMessages
-- ----------------------------
UPDATE "sqlite_sequence" SET seq = 11 WHERE name = 'GroupMessages';

-- ----------------------------
-- Indexes structure for table GroupMessages
-- ----------------------------
CREATE INDEX "IX_GroupMessages_FromUserId"
ON "GroupMessages" (
  "FromUserId" ASC
);
CREATE INDEX "IX_GroupMessages_GroupId_SentAt"
ON "GroupMessages" (
  "GroupId" ASC,
  "SentAt" ASC
);

-- ----------------------------
-- Auto increment value for Messages
-- ----------------------------
UPDATE "sqlite_sequence" SET seq = 7 WHERE name = 'Messages';

-- ----------------------------
-- Indexes structure for table Messages
-- ----------------------------
CREATE INDEX "IX_Messages_FromUserId_ToUserId_SentAt"
ON "Messages" (
  "FromUserId" ASC,
  "ToUserId" ASC,
  "SentAt" ASC
);
CREATE INDEX "IX_Messages_ToUserId"
ON "Messages" (
  "ToUserId" ASC
);

-- ----------------------------
-- Auto increment value for Users
-- ----------------------------
UPDATE "sqlite_sequence" SET seq = 7 WHERE name = 'Users';

-- ----------------------------
-- Indexes structure for table Users
-- ----------------------------
CREATE UNIQUE INDEX "IX_Users_Username"
ON "Users" (
  "Username" ASC
);

PRAGMA foreign_keys = true;
