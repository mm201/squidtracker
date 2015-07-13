# ************************************************************
# Sequel Pro SQL dump
# Version 4096
#
# http://www.sequelpro.com/
# http://code.google.com/p/sequel-pro/
#
# Host: 127.0.0.1 (MySQL 5.5.40)
# Database: squidtracker
# Generation Time: 2015-07-11 04:50:38 +0000
# ************************************************************


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;


# Dump of table squid_gear_clothes
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_gear_clothes`;

CREATE TABLE `squid_gear_clothes` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) NOT NULL DEFAULT '',
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_gear_head
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_gear_head`;

CREATE TABLE `squid_gear_head` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) NOT NULL DEFAULT '',
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_gear_shoes
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_gear_shoes`;

CREATE TABLE `squid_gear_shoes` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) NOT NULL DEFAULT '',
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_leaderboard_entries
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_leaderboard_entries`;

CREATE TABLE `squid_leaderboard_entries` (
  `leaderboard_id` int(10) unsigned NOT NULL,
  `position` int(10) unsigned NOT NULL,
  `mii_name` varchar(300) NOT NULL DEFAULT '',
  `weapon_id` int(10) unsigned DEFAULT NULL,
  `gear_shoes_id` int(10) unsigned DEFAULT NULL,
  `gear_clothes_id` int(10) unsigned DEFAULT NULL,
  `gear_head_id` int(10) unsigned DEFAULT NULL,
  PRIMARY KEY (`leaderboard_id`,`position`),
  KEY `weapon_id` (`weapon_id`),
  KEY `gear_shoes_id` (`gear_shoes_id`),
  KEY `gear_clothes_id` (`gear_clothes_id`),
  KEY `gear_head_id` (`gear_head_id`),
  CONSTRAINT `squid_leaderboard_entries_ibfk_5` FOREIGN KEY (`gear_head_id`) REFERENCES `squid_gear_head` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `squid_leaderboard_entries_ibfk_1` FOREIGN KEY (`leaderboard_id`) REFERENCES `squid_leaderboards` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `squid_leaderboard_entries_ibfk_2` FOREIGN KEY (`weapon_id`) REFERENCES `squid_weapons` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `squid_leaderboard_entries_ibfk_3` FOREIGN KEY (`gear_shoes_id`) REFERENCES `squid_gear_shoes` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `squid_leaderboard_entries_ibfk_4` FOREIGN KEY (`gear_clothes_id`) REFERENCES `squid_gear_clothes` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_leaderboards
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_leaderboards`;

CREATE TABLE `squid_leaderboards` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `term_begin` datetime NOT NULL,
  `term_end` datetime NOT NULL,
  `stage1_id` int(10) unsigned DEFAULT NULL,
  `stage2_id` int(10) unsigned DEFAULT NULL,
  `stage3_id` int(10) unsigned DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `term_begin` (`term_begin`),
  KEY `stage1_id` (`stage1_id`),
  KEY `stage2_id` (`stage2_id`),
  KEY `stage3_id` (`stage3_id`),
  CONSTRAINT `squid_leaderboards_ibfk_3` FOREIGN KEY (`stage3_id`) REFERENCES `squid_stages` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `squid_leaderboards_ibfk_1` FOREIGN KEY (`stage1_id`) REFERENCES `squid_stages` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `squid_leaderboards_ibfk_2` FOREIGN KEY (`stage2_id`) REFERENCES `squid_stages` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_logs_stages_info
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_logs_stages_info`;

CREATE TABLE `squid_logs_stages_info` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `date` datetime NOT NULL,
  `data` text NOT NULL,
  PRIMARY KEY (`id`),
  KEY `date` (`date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_stages
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_stages`;

CREATE TABLE `squid_stages` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) NOT NULL DEFAULT '',
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;



# Dump of table squid_weapons
# ------------------------------------------------------------

DROP TABLE IF EXISTS `squid_weapons`;

CREATE TABLE `squid_weapons` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `identifier` varchar(80) NOT NULL DEFAULT '',
  `name_ja` varchar(300) DEFAULT NULL,
  `name_en` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `identifier` (`identifier`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;




/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;
/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
