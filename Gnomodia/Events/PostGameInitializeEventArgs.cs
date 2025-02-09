﻿/*
 *  Gnomodia
 *
 *  Copyright © 2014 Alexander Krivács Schrøder (https://alexanderschroeder.net/)
 *
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU Lesser General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU Lesser General Public License
 *   along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

namespace Gnomodia.Events
{
    /// <summary>
    /// This is the event where you can be certain all other mods have initialized,
    /// so that you may safely access their provided features.
    /// </summary>
    public class PostGameInitializeEventArgs : GameInitializeEventArgs
    {
    }
}