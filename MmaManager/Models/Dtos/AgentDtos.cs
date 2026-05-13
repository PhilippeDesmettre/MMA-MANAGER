namespace MmaManager.Models.Dtos;

public record AgentDto(
    int     AgentID,
    string  Prenom,
    string  Nom,
    bool    EstJoueur,
    int     CompContact,
    int     CompNegociation,
    int     CompReseau,
    int     CompReputation,
    int     CompInfluence,
    int     CompMarketing,
    int     CompJuridique,
    decimal SalaireMensuel,
    int     MaxCombattants,
    int     NbCombattantsActuels
);

public record AssignerAgentRequest(int? AgentID);
