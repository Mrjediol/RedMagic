using System;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// El "fallar alto" que pide docs/schemas/COMPATIBILITY.md: un assetRef que no resuelve, una
    /// capa que no existe, un 'type' de BossAttack que no es ninguna clase del proyecto, o
    /// phases[0].startsAtHealth distinto de 1 no deben producir un asset a medias ni callarse
    /// detrás de un valor por defecto — deben parar el import entero con un mensaje claro.
    ///
    /// La capturan los tres importadores desde <see cref="ConfigImportRunner"/>, que la vuelca en
    /// <see cref="ConfigImportReport.Errors"/> sin haber dejado ningún asset a medio escribir: cada
    /// importador resuelve y valida TODO antes de crear o tocar un asset (ver el comentario de
    /// "Pase 1 / Pase 2" en <c>BossConfigImporter</c> y <c>EnemyConfigImporter</c>).
    /// </summary>
    public sealed class ConfigImportException : Exception
    {
        public ConfigImportException(string message) : base(message)
        {
        }
    }
}
